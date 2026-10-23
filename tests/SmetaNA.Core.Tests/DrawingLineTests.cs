using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструменты «Линия» и «Кривая»: сценарии ToolInput (клики/штрих), замыкание
/// контура с гранью, VCB (точные длины), привязки, undo/redo, отмена.
/// </summary>
public class DrawingLineTests
{
    [Fact]
    public void Line_ClicksAndEnter_CommitsOpenChainAsOneCommand()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 40, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(4, mesh.HalfEdges.Count);
        Assert.Empty(mesh.Faces);
        DrawingTestSupport.PointsEqual(
            [new Vector3d(10, 10, 0), new Vector3d(50, 10, 0), new Vector3d(50, 40, 0)],
            DrawingTestSupport.Vertices(mesh),
            1e-9);
        Assert.True(undo.CanUndo);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
        undo.Redo();
        Assert.Single(scene.Root.Children);
    }

    [Fact]
    public void Line_CloseByClickNearStart_CreatesPlanarFace()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 40, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(10.3, 10.3, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(6, mesh.HalfEdges.Count);
        MeshFace face = Assert.Single(mesh.Faces);
        Assert.Equal(3, mesh.GetRing(face).Count);
        Assert.False(mesh.IsWatertight, "Одна грань с границей не делает меш замкнутым.");

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Line_VcbLength_CommitsExactSegmentLength()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(60, 10, 0));
        Assert.True(tool.TryApplyValue(100));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        (Vector3d From, Vector3d To) edge = Assert.Single(DrawingTestSupport.Edges(mesh));
        Assert.Equal(100, (edge.To - edge.From).Length(), 9);
        DrawingTestSupport.PointEqual(new Vector3d(10, 10, 0), edge.From, 1e-9);
    }

    [Fact]
    public void Line_VcbLength_WithoutMove_UsesLastDirection()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.MoveTo(context, tool, new Vector3d(60, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(60, 10, 0));
        Assert.True(tool.TryApplyValue(25));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointEqual(new Vector3d(85, 10, 0), DrawingTestSupport.Vertices(mesh)[^1], 1e-9);
    }

    [Fact]
    public void Line_Delete_RemovesLastPointOfChain()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 40, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Delete);
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(2, mesh.Vertices.Count);
        Assert.Single(DrawingTestSupport.Edges(mesh));
    }

    [Fact]
    public void Line_Escape_CancelsWithoutSceneChanges()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(10, 10, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 10, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Escape);

        Assert.Empty(scene.Root.Children);
        Assert.False(undo.CanUndo);
        Assert.False(tool.IsInteracting);
    }

    [Fact]
    public void Line_SnapsToExistingEndpoint_Exactly()
    {
        Scene scene = new();
        var wireMesh = new HalfEdgeMesh();
        MeshVertex a = wireMesh.AddVertex(new Vector3d(20, 20, 0));
        MeshVertex b = wireMesh.AddVertex(new Vector3d(80, 20, 0));
        wireMesh.AddWireEdge(a, b);
        scene.Root.AddChild(new MeshNode { Mesh = wireMesh, Name = "Ребро" });

        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        // клик в 4 px от конца ребра (20 px — допуск привязки 8 px) — конец берётся точно
        DrawingTestSupport.ClickPx(context, tool, DrawingTestSupport.Px(context, new Vector3d(79, 20, 0)));
        DrawingTestSupport.Click(context, tool, new Vector3d(80, 60, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointEqual(new Vector3d(80, 20, 0), DrawingTestSupport.Vertices(mesh)[0], 0);
    }

    [Fact]
    public void Line_SnapsToMidpoint_Exactly()
    {
        Scene scene = new();
        var wireMesh = new HalfEdgeMesh();
        MeshVertex a = wireMesh.AddVertex(new Vector3d(20, 20, 0));
        MeshVertex b = wireMesh.AddVertex(new Vector3d(80, 20, 0));
        wireMesh.AddWireEdge(a, b);
        scene.Root.AddChild(new MeshNode { Mesh = wireMesh, Name = "Ребро" });

        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.ClickPx(context, tool, DrawingTestSupport.Px(context, new Vector3d(50, 21, 0)));
        DrawingTestSupport.Click(context, tool, new Vector3d(50, 60, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointEqual(new Vector3d(50, 20, 0), DrawingTestSupport.Vertices(mesh)[0], 0);
    }

    [Fact]
    public void Line_UsesSnapSolverCandidates_AsConstructionPoints()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        var expected = new Queue<Vector3d>(
        [
            new Vector3d(10, 10, 0),
            new Vector3d(30, 20, 0),
        ]);
        var requests = new List<SnapRequest>();
        context.SnapSolver = request =>
        {
            requests.Add(request);
            return new SnapResult(
            [
                new SnapCandidate(SnapType.Perpendicular, expected.Dequeue(), 1.0, 280, SnapMarkerKind.Line),
            ]);
        };

        DrawingTestSupport.Click(context, tool, new Vector3d(5, 5, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(35, 5, 0));
        DrawingTestSupport.Key(context, tool, ToolKey.Enter);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        DrawingTestSupport.PointsEqual(
            [new Vector3d(10, 10, 0), new Vector3d(30, 20, 0)],
            DrawingTestSupport.Vertices(mesh));
        Assert.Equal(2, requests.Count);
        DrawingTestSupport.PointEqual(new Vector3d(10, 10, 0), requests[1].Anchor!.Value);
    }

    [Fact]
    public void Freehand_Drag_CommitsStrokeAsOneCommand()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new FreehandTool();
        tool.Activate(context);

        DrawingTestSupport.DragStroke(
            context,
            tool,
            [
                new Vector3d(10, 10, 0),
                new Vector3d(30, 10, 0),
                new Vector3d(50, 20, 0),
                new Vector3d(70, 20, 0),
            ]);

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(6, mesh.HalfEdges.Count);
        Assert.Empty(mesh.Faces);
        DrawingTestSupport.PointEqual(new Vector3d(10, 10, 0), DrawingTestSupport.Vertices(mesh)[0], 1e-9);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Freehand_ReleaseNearStart_ClosesStrokeWithFace()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new FreehandTool();
        tool.Activate(context);

        DrawingTestSupport.DragStroke(
            context,
            tool,
            [
                new Vector3d(10, 10, 0),
                new Vector3d(50, 10, 0),
                new Vector3d(50, 50, 0),
                new Vector3d(10, 50, 0),
            ],
            releaseAt: new Vector3d(10.5, 10.5, 0));

        HalfEdgeMesh mesh = DrawingTestSupport.ShapeMeshOf(scene);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Single(mesh.Faces);
        DrawingTestSupport.PointEqual(new Vector3d(10, 10, 0), DrawingTestSupport.Vertices(mesh)[0], 1e-9);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Freehand_Escape_CancelsStroke()
    {
        Scene scene = new();
        (ToolContext context, UndoStack undo) = DrawingTestSupport.Setup(scene);
        var tool = new FreehandTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(DrawingTestSupport.Px(context, new Vector3d(10, 10, 0)), PointerButton.Left));
        tool.OnPointer(PointerInput.Move(DrawingTestSupport.Px(context, new Vector3d(30, 10, 0))));
        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.Empty(scene.Root.Children);
        Assert.False(undo.CanUndo);
        Assert.False(tool.IsInteracting);
    }

    [Fact]
    public void Freehand_VcbValue_IsNotApplied()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new FreehandTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(100));
    }
}
