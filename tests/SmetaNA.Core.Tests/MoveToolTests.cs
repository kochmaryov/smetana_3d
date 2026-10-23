using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент Move/Copy: перенос грани со склеиванием, перенос объекта (трансформация
/// узла), копия объекта (Ctrl), точный ввод VCB, undo/redo.
/// </summary>
public class MoveToolTests
{
    private static (ToolContext Context, UndoStack Undo) Setup(out HalfEdgeMesh mesh, out MeshNode node)
    {
        mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        return (context, undo);
    }

    private static Dictionary<MeshVertex, Vector3d> Snapshot(HalfEdgeMesh mesh)
        => mesh.Vertices.ToDictionary(vertex => vertex, vertex => vertex.Position);

    [Fact]
    public void Tool_FaceMove_VcbMovesRingExactly()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));

        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.TryApplyValue(3));

        // направление задаёт перетаскивание вдоль X (камера смотрит по Y) — смещение ровно 3 мм
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(before[vertex] + new Vector3d(3, 0, 0), vertex.Position, 1e-6));
        }

        Assert.True(mesh.IsWatertight);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Tool_FaceMove_DragCommitsTranslation()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(6, 5, 10));

        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));

        Assert.Equal(4, tool.Translation.X, 6);
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(before[vertex] + new Vector3d(4, 0, 0), vertex.Position, 1e-6));
        }
    }

    [Fact]
    public void Tool_MoveUndoRedo_RestoresPositions()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(3);

        undo.Undo();
        foreach ((MeshVertex vertex, Vector3d position) in before)
        {
            Assert.True(ToolsTestSupport.NearlyEqual(position, vertex.Position, 1e-9));
        }

        undo.Redo();
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(before[vertex] + new Vector3d(3, 0, 0), vertex.Position, 1e-6));
        }
    }

    [Fact]
    public void Tool_ObjectMove_ChangesNodeTransform()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.IsObjectMode);
        Assert.True(tool.TryApplyValue(3));

        Assert.True(ToolsTestSupport.NearlyEqual(
            new Vector3d(3, 0, 0),
            node.LocalTransform.TransformPoint(Vector3d.Zero)));

        undo.Undo();
        Assert.True(ToolsTestSupport.NearlyEqual(Vector3d.Zero, node.LocalTransform.TransformPoint(Vector3d.Zero)));

        undo.Redo();
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(3, 0, 0), node.LocalTransform.TransformPoint(Vector3d.Zero)));
    }

    [Fact]
    public void Tool_ObjectCopy_CtrlAddsDuplicate()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left, ToolModifiers.Ctrl));
        tool.OnPointer(PointerInput.Move(dragPixel));
        Assert.True(tool.IsCopyMode);
        Assert.True(tool.OnPointer(PointerInput.Release(dragPixel, PointerButton.Left)));

        SceneNode parent = node.Parent!;
        Assert.Equal(2, parent.Children.Count);

        SceneNode copy = parent.Children[^1];
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(3, 0, 0), copy.LocalTransform.TransformPoint(Vector3d.Zero)));
        Assert.True(ToolsTestSupport.NearlyEqual(Vector3d.Zero, node.LocalTransform.TransformPoint(Vector3d.Zero)));

        undo.Undo();
        Assert.Single(parent.Children);
        Assert.True(ToolsTestSupport.NearlyEqual(Vector3d.Zero, node.LocalTransform.TransformPoint(Vector3d.Zero)));
    }

    [Fact]
    public void Tool_VcbWithoutDrag_NotApplied()
    {
        (ToolContext context, _) = Setup(out _, out _);
        var tool = new MoveTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(5));
    }

    [Fact]
    public void Tool_Escape_CancelsWithoutChanges()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(7, 5, 10))));
        tool.OnKey(new KeyInput(ToolKey.Escape));

        Assert.False(tool.IsInteracting);
        Assert.False(undo.CanUndo);
        foreach ((MeshVertex vertex, Vector3d position) in before)
        {
            Assert.True(ToolsTestSupport.NearlyEqual(position, vertex.Position, 1e-9));
        }
    }

    [Fact]
    public void Tool_FaceMove_PullsGluedNeighborVertices()
    {
        HalfEdgeMesh mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(mesh);
        // соседний меш с совпадающими вершинами (позиционное склеивание, stickiness)
        HalfEdgeMesh neighbour = ToolsTestSupport.Box(10, 10, 10);
        scene.Root.AddChild(new MeshNode { Mesh = neighbour, Name = "Neighbour" });
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new MoveTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.TryApplyValue(3);

        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            // склеенная вершина соседнего меша перенесена вместе с вершиной грани
            Assert.Contains(neighbour.Vertices, glued => ToolsTestSupport.NearlyEqual(glued.Position, vertex.Position, 1e-6));
        }
    }
}
