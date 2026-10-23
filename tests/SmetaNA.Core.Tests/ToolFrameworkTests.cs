using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Каркас инструментов: команды меша, контекст, выборка, привязки, VCB.</summary>
public class ToolFrameworkTests
{
    [Fact]
    public void MoveVerticesCommand_AppliesAndRevertsPositions()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 3);
        var command = new MoveVerticesCommand("Перенос вершин", moves);

        command.Execute();
        Assert.Equal(13, mesh.GetRing(top)[0].Position.Z, 9);

        command.Undo();
        Assert.Equal(10, mesh.GetRing(top)[0].Position.Z, 9);
    }

    [Fact]
    public void CompositeEditCommand_ExecutesInOrder_UndoesInReverse()
    {
        var log = new List<string>();
        var first = new RecordingCommand("первый", () => log.Add("do1"), () => log.Add("undo1"));
        var second = new RecordingCommand("второй", () => log.Add("do2"), () => log.Add("undo2"));
        var composite = new CompositeEditCommand("группа", [first, second]);

        composite.Execute();
        composite.Undo();

        Assert.Equal(new[] { "do1", "do2", "undo2", "undo1" }, log);
    }

    [Fact]
    public void ToolContext_Execute_PushesHistory_AndRaisesSceneEdited()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        int edited = 0;
        context.SceneEdited += () => edited++;

        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 2);
        context.Execute(new MoveVerticesCommand("Push/Pull 2 mm", moves));

        Assert.True(undo.CanUndo);
        Assert.Equal(1, edited);
        Assert.Equal("Push/Pull 2 mm", undo.NextUndoDescription);
    }

    [Fact]
    public void ToolContext_Snap_UsesInjectedSolver()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        InferenceViewport viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -50, 5), new Vector3d(5, 5, 5));
        SnapRequest? seen = null;
        var expected = new SnapResult(
        [
            new SnapCandidate(SnapType.Endpoint, new Vector3d(1, 2, 3), 1, 10, SnapMarkerKind.Cube),
        ]);
        context.SnapSolver = request =>
        {
            seen = request;
            return expected;
        };

        SnapRequest request = SnapRequest.AtCursor(viewport, context.BuildInferenceScene(), new Vector2d(400, 300));
        SnapResult result = context.Snap(request);

        Assert.Same(request, seen);
        Assert.Same(expected, result);
        Assert.Equal(new Vector3d(1, 2, 3), result.Best!.Point);
    }

    [Fact]
    public void ToolContext_BuildInferenceScene_MapsSceneFaces()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);

        InferenceScene snapshot = context.BuildInferenceScene();

        Assert.Equal(6, snapshot.Faces.Count);
    }

    [Fact]
    public void ToolContext_TryPickFace_PicksNearestFaceUnderCursor()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -50, 5), new Vector3d(5, 5, 5));

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 10, 5));
        bool picked = context.TryPickFace(pixel, out ToolFaceHit hit);

        // ближайшая к камере грань на этом луче — передняя (y = 0, нормаль −Y)
        Assert.True(picked);
        Assert.Same(node, hit.Node);
        Assert.True(ToolsTestSupport.NearlyEqual(
            MeshNormals.ComputeFaceNormal(hit.Face).Normalized(), -Vector3d.UnitY));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(5, 0, 5), hit.Point, 1e-6));
    }

    [Fact]
    public void ToolContext_TryPickFace_MissesEmptySpace()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -50, 5), new Vector3d(5, 5, 5));

        bool picked = context.TryPickFace(new Vector2d(5, 5), out _);

        Assert.False(picked);
    }

    [Fact]
    public void ToolContext_TryPickFace_WithoutViewport_Fails()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);

        Assert.False(context.TryPickFace(new Vector2d(100, 100), out _));
    }

    [Fact]
    public void ToolSelection_TracksFaceAndNode()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        var selection = new ToolSelection();
        int changed = 0;
        selection.Changed += () => changed++;
        MeshFace face = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);

        selection.SetFace(new MeshFaceRef(node, face));
        Assert.Same(node, selection.Node);
        Assert.Equal(face, selection.Face?.Face);
        Assert.Equal(1, changed);

        selection.Clear();
        Assert.Null(selection.Node);
        Assert.Null(selection.Face);
        Assert.Equal(2, changed);
    }

    [Fact]
    public void ToolContext_ReportVcb_PublishesState()
    {
        var scene = new Scene();
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        int changed = 0;
        context.VcbStateChanged += () => changed++;

        context.ReportVcb(ToolValueKind.Length, 25, true);

        Assert.Equal(1, changed);
        Assert.True(context.VcbState.IsActive);
        Assert.Equal(ToolValueKind.Length, context.VcbState.Kind);
        Assert.Equal<double?>(25, context.VcbState.Value);
    }

    private sealed class RecordingCommand(string description, Action execute, Action undo) : IEditCommand
    {
        public string Description => description;

        public void Execute() => execute();

        public void Undo() => undo();
    }
}
