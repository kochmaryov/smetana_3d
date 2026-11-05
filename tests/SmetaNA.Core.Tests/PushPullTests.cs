using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Push/Pull: выдавливание/вдавливание грани (объём, топология, watertight),
/// числовое ввод VCB, мышиной сценарий, undo/redo и отмена инструмента.
/// </summary>
public class PushPullTests
{
    [Fact]
    public void PushTopFace_IncreasesVolume_PreservesTopologyAndWatertight()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 20, 30);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        (int Vertices, int Edges, int Faces) before = ToolsTestSupport.Topology(mesh);
        var undo = new UndoStack();

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 5);
        undo.Push(new MoveVerticesCommand("Push/Pull 5 mm", moves));

        Assert.Equal(10 * 20 * 35, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.Equal(before, ToolsTestSupport.Topology(mesh));
        Assert.True(mesh.IsWatertight);
        foreach (MeshVertex vertex in mesh.GetRing(top))
        {
            Assert.Equal(35, vertex.Position.Z, 9);
        }
    }

    [Fact]
    public void PullTopFace_DecreasesVolume()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 20, 30);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var undo = new UndoStack();

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, -5);
        undo.Push(new MoveVerticesCommand("Push/Pull -5 mm", moves));

        Assert.Equal(10 * 20 * 25, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void PushSideFace_ExpandsAlongItsNormal()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 20, 30);
        MeshFace side = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitX);
        var undo = new UndoStack();

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, side, 7);
        undo.Push(new MoveVerticesCommand("Push/Pull 7 mm", moves));

        Assert.Equal(17 * 20 * 30, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void PushPull_UndoRedo_RestoresGeometry()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var undo = new UndoStack();
        double initialVolume = ToolsTestSupport.SignedVolume(mesh);

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanFaceMoveAlongNormal(mesh, top, 4);
        undo.Push(new MoveVerticesCommand("Push/Pull 4 mm", moves));

        Assert.Equal(10 * 10 * 14, ToolsTestSupport.SignedVolume(mesh), 6);

        undo.Undo();
        Assert.Equal(initialVolume, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.Equal(10, mesh.GetRing(top)[0].Position.Z, 9);

        undo.Redo();
        Assert.Equal(10 * 10 * 14, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.Equal(14, mesh.GetRing(top)[0].Position.Z, 9);
    }

    [Fact]
    public void Tool_VcbValue_CommitsExactDistance()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        // камера над кубом: ближайшая грань под курсором — верхняя (нормаль +Z)
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left)));
        Assert.True(tool.TryApplyValue(12.5));

        Assert.Equal(10 * 10 * 22.5, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.True(mesh.IsWatertight);
        Assert.True(undo.CanUndo);
        Assert.False(tool.IsInteracting);

        undo.Undo();
        Assert.Equal(10 * 10 * 10, ToolsTestSupport.SignedVolume(mesh), 6);
    }

    [Fact]
    public void Tool_HoverThenVcbValue_PushesSelectedFace()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.NotNull(context.Selection.Face);

        Assert.True(tool.TryApplyValue(4));
        Assert.Equal(10 * 10 * 14, ToolsTestSupport.SignedVolume(mesh), 6);

        // грань остаётся выбранной — повторное значение продолжает выдавливание
        Assert.True(tool.TryApplyValue(1));
        Assert.Equal(10 * 10 * 15, ToolsTestSupport.SignedVolume(mesh), 6);
    }

    [Fact]
    public void Tool_MouseDrag_CommitsDistanceFromPointer()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));

        // курсор наведён на точку оси выдавливания (5, 5, 12) — расстояние ровно 2
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 12));
        Assert.True(tool.OnPointer(PointerInput.Move(dragPixel)));
        Assert.Equal(2, tool.Distance, 6);

        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));
        Assert.Equal(10 * 10 * 12, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void Tool_CancelMidDrag_LeavesMeshUnchanged()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 12));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.IsInteracting);

        tool.Cancel();

        Assert.False(tool.IsInteracting);
        Assert.True(tool.Preview.IsEmpty);
        Assert.Equal(10 * 10 * 10, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Tool_EscapeKey_CancelsInteraction()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Escape)));

        Assert.False(tool.IsInteracting);
        Assert.Equal(10 * 10 * 10, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Tool_Deactivate_CancelsInteraction()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.Deactivate();

        Assert.True(tool.Preview.IsEmpty);
        Assert.Equal(ToolVcbState.Inactive, tool.Vcb);
        Assert.Equal(10 * 10 * 10, ToolsTestSupport.SignedVolume(mesh), 6);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Tool_DragPreview_ShowsOnlyMovingFace()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 15));
        tool.OnPointer(PointerInput.Move(dragPixel));

        // только смещаемая грань (4 ребра) + ось выдавливания (Highlight),
        // следы вершин (Guide) и ни одного контура соседних/противоположной граней
        int ringCount = mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)).Count;
        Assert.Equal(ringCount + 1, tool.Preview.Lines.Count(line => line.Style == ToolPreviewStyle.Highlight));
        Assert.Equal(ringCount, tool.Preview.Lines.Count(line => line.Style == ToolPreviewStyle.Guide));
        Assert.DoesNotContain(tool.Preview.Lines, line => line.Style == ToolPreviewStyle.Default);
        Assert.Single(tool.Preview.Points);
        Assert.Single(tool.Preview.Labels);
    }

    [Fact]
    public void Tool_DragBoundaryFace_BuildsWatertightSolid()
    {
        // замкнутый контур без соседних граней — как нарисованный прямоугольник
        var mesh = new HalfEdgeMesh();
        MeshVertex v0 = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex v1 = mesh.AddVertex(new Vector3d(10, 0, 0));
        MeshVertex v2 = mesh.AddVertex(new Vector3d(10, 10, 0));
        MeshVertex v3 = mesh.AddVertex(new Vector3d(0, 10, 0));
        MeshFace face = mesh.AddFace([v0, v1, v2, v3]);
        face.MaterialIndex = 3;

        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 0));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 0));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 5));
        Assert.True(tool.OnPointer(PointerInput.Move(dragPixel)));
        Assert.Equal(5, tool.Distance, 6);
        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));

        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(8, mesh.Vertices.Count);
        Assert.True(mesh.IsWatertight);
        Assert.Equal(10 * 10 * 5, ToolsTestSupport.SignedVolume(mesh), 6);

        // исходная грань — верхняя крышка (перенесена на 5 мм), основание в z = 0
        double topZ = double.MinValue;
        double bottomZ = double.MaxValue;
        foreach (MeshVertex vertex in mesh.Vertices)
        {
            topZ = Math.Max(topZ, vertex.Position.Z);
            bottomZ = Math.Min(bottomZ, vertex.Position.Z);
        }

        Assert.Equal(5, topZ, 9);
        Assert.Equal(0, bottomZ, 9);
        Assert.True(undo.CanUndo);
        foreach (MeshFace created in mesh.Faces)
        {
            Assert.Equal(3, created.MaterialIndex);
        }

        undo.Undo();
        Assert.Single(mesh.Faces);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(0, mesh.GetRing(face)[0].Position.Z, 9);
    }

    [Fact]
    public void Tool_VcbBoundaryFace_BuildsSolid_UndoRedoRestores()
    {
        (HalfEdgeMesh mesh, MeshFace face) = ToolsTestSupport.DrawnQuad(10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 0));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 0));
        tool.OnPointer(PointerInput.Move(pixel));
        Assert.True(tool.TryApplyValue(12.5));

        Assert.True(mesh.IsWatertight);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(10 * 10 * 12.5, ToolsTestSupport.SignedVolume(mesh), 6);

        undo.Undo();
        Assert.Single(mesh.Faces);
        Assert.Equal(4, mesh.Vertices.Count);
        Assert.False(mesh.IsWatertight);
        Assert.Equal(8, mesh.HalfEdges.Count);
        Assert.Equal(0, mesh.GetRing(face)[1].Position.Z, 9);

        undo.Redo();
        Assert.True(mesh.IsWatertight);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(10 * 10 * 12.5, ToolsTestSupport.SignedVolume(mesh), 6);
    }

    [Fact]
    public void ExtrudeBoundaryFace_NegativeDistance_BuildsSolidBelow()
    {
        (HalfEdgeMesh mesh, MeshFace face) = ToolsTestSupport.DrawnQuad(10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);

        var command = new ExtrudeFaceCommand(mesh, face, -4);
        undo.Push(command);

        Assert.True(mesh.IsWatertight);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(10 * 10 * 4, ToolsTestSupport.SignedVolume(mesh), 6);
        foreach (MeshVertex vertex in mesh.GetRing(face))
        {
            Assert.Equal(0, vertex.Position.Z, 9);
        }

        undo.Undo();
        Assert.Single(mesh.Faces);
        Assert.Equal(0, mesh.GetRing(face)[0].Position.Z, 9);
    }

    [Fact]
    public void Tool_ZeroDistance_CreatesNoHistoryEntry()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new PushPullTool();
        tool.Activate(context);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        Assert.True(tool.TryApplyValue(0));

        Assert.False(undo.CanUndo);
        Assert.Equal(10 * 10 * 10, ToolsTestSupport.SignedVolume(mesh), 6);
    }
}
