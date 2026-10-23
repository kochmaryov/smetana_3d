using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент Rotate: вращение грани вокруг нормали (точный угол VCB, перетаскивание),
/// вращение объекта (трансформация узла), undo/redo.
/// </summary>
public class RotateToolTests
{
    private static readonly Vector3d Pivot = new(5, 10, 30);

    private static (ToolContext Context, UndoStack Undo) Setup(out HalfEdgeMesh mesh, out MeshNode node)
    {
        mesh = ToolsTestSupport.Box(10, 20, 30);
        (Scene scene, node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -50, 45), new Vector3d(5, 10, 30));
        return (context, undo);
    }

    private static Dictionary<MeshVertex, Vector3d> Snapshot(HalfEdgeMesh mesh)
        => mesh.Vertices.ToDictionary(vertex => vertex, vertex => vertex.Position);

    [Fact]
    public void Tool_VcbAngle_RotatesRingExactly()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        Assert.True(tool.TryApplyValue(90));

        Transform3d expected = EditingTransforms.CreateRotation(Vector3d.UnitZ, Pivot, Math.PI / 2);
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(expected.TransformPoint(before[vertex]), vertex.Position, 1e-6));
        }

        Assert.Equal(90, tool.AngleDegrees, 9);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Tool_DragAngle_MeasuresNinetyDegrees()
    {
        (ToolContext context, _) = Setup(out _, out _);
        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 6, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));

        // опорное направление (-4, 0), текущее (0, -4) — поворот +90° против часовой
        Assert.Equal(90, tool.AngleDegrees, 6);
    }

    [Fact]
    public void Tool_RotateUndoRedo_RestoresPositions()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.TryApplyValue(45);

        undo.Undo();
        foreach ((MeshVertex vertex, Vector3d position) in before)
        {
            Assert.True(ToolsTestSupport.NearlyEqual(position, vertex.Position, 1e-9));
        }

        undo.Redo();
        Transform3d expected = EditingTransforms.CreateRotation(Vector3d.UnitZ, Pivot, Math.PI / 4);
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(expected.TransformPoint(before[vertex]), vertex.Position, 1e-6));
        }
    }

    [Fact]
    public void Tool_ObjectRotate_ChangesNodeTransform()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);
        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        Assert.True(tool.IsObjectMode);
        Assert.True(tool.TryApplyValue(90));

        Transform3d expected = EditingTransforms.CreateRotation(Vector3d.UnitZ, Pivot, Math.PI / 2);
        Assert.True(ToolsTestSupport.NearlyEqual(
            expected.TransformPoint(new Vector3d(0, 0, 30)),
            node.LocalTransform.TransformPoint(new Vector3d(0, 0, 30)),
            1e-6));

        undo.Undo();
        Assert.True(ToolsTestSupport.NearlyEqual(
            new Vector3d(0, 0, 30),
            node.LocalTransform.TransformPoint(new Vector3d(0, 0, 30)),
            1e-9));
    }

    [Fact]
    public void Tool_Enter_CommitsDrag()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 6, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));
        tool.OnKey(new KeyInput(ToolKey.Enter));

        Assert.False(tool.IsInteracting);
        Assert.True(undo.CanUndo);
        Assert.Contains(
            mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)),
            vertex => ToolsTestSupport.NearlyEqual(new Vector3d(15, 5, 30), vertex.Position, 1e-3));
    }

    [Fact]
    public void Tool_VcbWithoutPress_NotApplied()
    {
        (ToolContext context, _) = Setup(out _, out _);
        var tool = new RotateTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyValue(90));
    }
}
