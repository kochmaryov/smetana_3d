using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент Scale: равномерное масштабирование грани вокруг центроида
/// (точный множитель VCB, перетаскивание), масштабирование объекта, undo/redo.
/// </summary>
public class ScaleToolTests
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
    public void Tool_VcbFactor_ScalesRingExactly()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new ScaleTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));
        Assert.True(tool.TryApplyValue(2));

        Transform3d expected = EditingTransforms.CreateUniformScale(Pivot, 2);
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(expected.TransformPoint(before[vertex]), vertex.Position, 1e-6));
        }

        Assert.Equal(2, tool.Factor, 9);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Tool_DragFactor_MeasuresRadiusRatio()
    {
        (ToolContext context, _) = Setup(out _, out _);
        var tool = new ScaleTool();
        tool.Activate(context);

        // опорный радиус 4 мм (точка (1,10,30) от центроида (5,10,30)); курсор на 8 мм
        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        Vector2d dragPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(13, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Move(dragPixel));

        Assert.Equal(2, tool.Factor, 6);
    }

    [Fact]
    public void Tool_VcbNonPositiveFactor_NotApplied()
    {
        (ToolContext context, _) = Setup(out _, out _);
        var tool = new ScaleTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));

        Assert.False(tool.TryApplyValue(0));
        Assert.False(tool.TryApplyValue(-2));
    }

    [Fact]
    public void Tool_ScaleUndoRedo_RestoresPositions()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh, out _);
        Dictionary<MeshVertex, Vector3d> before = Snapshot(mesh);
        var tool = new ScaleTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        tool.TryApplyValue(0.5);

        undo.Undo();
        foreach ((MeshVertex vertex, Vector3d position) in before)
        {
            Assert.True(ToolsTestSupport.NearlyEqual(position, vertex.Position, 1e-9));
        }

        undo.Redo();
        Transform3d expected = EditingTransforms.CreateUniformScale(Pivot, 0.5);
        foreach (MeshVertex vertex in mesh.GetRing(ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ)))
        {
            Assert.True(ToolsTestSupport.NearlyEqual(expected.TransformPoint(before[vertex]), vertex.Position, 1e-6));
        }
    }

    [Fact]
    public void Tool_ObjectScale_ChangesNodeTransform()
    {
        (ToolContext context, UndoStack undo) = Setup(out _, out MeshNode node);
        context.Selection.SetNode(node);
        var tool = new ScaleTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(1, 10, 30));
        tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left));
        Assert.True(tool.IsObjectMode);
        Assert.True(tool.TryApplyValue(2));

        Transform3d expected = EditingTransforms.CreateUniformScale(Pivot, 2);
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
}
