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
