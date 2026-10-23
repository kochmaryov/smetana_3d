using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент Soften/Smooth: сглаживание рёбер по порогу двугранного угла (VCB),
/// снятие сглаживания (Ctrl), undo/redo.
/// </summary>
public class SoftenSmoothToolTests
{
    private static (ToolContext Context, UndoStack Undo) Setup(out HalfEdgeMesh mesh)
    {
        mesh = ToolsTestSupport.Box(10, 10, 10);
        (Scene scene, _) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        return (context, undo);
    }

    [Fact]
    public void Tool_Vcb_SetsSmoothingThreshold()
    {
        (ToolContext context, _) = Setup(out _);
        var tool = new SoftenSmoothTool();
        tool.Activate(context);

        Assert.Equal(ToolValueKind.Angle, tool.Vcb.Kind);
        Assert.True(tool.TryApplyValue(120));
        Assert.Equal(120, tool.ThresholdDegrees);

        Assert.False(tool.TryApplyValue(200));
        Assert.False(tool.TryApplyValue(-5));
        Assert.Equal(120, tool.ThresholdDegrees);
    }

    [Fact]
    public void Tool_Stroke_SmoothsEdgesWithinThreshold()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        MeshFace bottom = ToolsTestSupport.FaceWithNormal(mesh, -Vector3d.UnitZ);
        var tool = new SoftenSmoothTool();
        tool.Activate(context);
        tool.TryApplyValue(120);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        // все 4 ребра верхней грани сглажены с обеих сторон
        MeshHalfEdge edge = top.HalfEdge;
        do
        {
            Assert.True(edge.IsSmooth);
            Assert.True(edge.IsSoft);
            Assert.True(edge.Twin!.IsSmooth);
            edge = edge.Next!;
        }
        while (edge != top.HalfEdge);

        // рёбра дна не тронуты
        Assert.False(bottom.HalfEdge.IsSmooth);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void Tool_ThresholdBelowAngle_DoesNotSmooth()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new SoftenSmoothTool();
        tool.Activate(context);

        // порог по умолчанию 20° меньше двугранного угла куба (90°)
        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        Assert.False(top.HalfEdge.IsSmooth);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Tool_CtrlStroke_UnsmoothsEdges()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new SoftenSmoothTool();
        tool.Activate(context);
        tool.TryApplyValue(120);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));
        Assert.True(top.HalfEdge.IsSmooth);

        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left, ToolModifiers.Ctrl));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        MeshHalfEdge edge = top.HalfEdge;
        do
        {
            Assert.False(edge.IsSmooth);
            Assert.False(edge.IsSoft);
            Assert.False(edge.Twin!.IsSmooth);
            edge = edge.Next!;
        }
        while (edge != top.HalfEdge);
    }

    [Fact]
    public void Tool_SmoothUndoRedo_RestoresFlags()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        var tool = new SoftenSmoothTool();
        tool.Activate(context);
        tool.TryApplyValue(120);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 10));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        undo.Undo();
        Assert.False(top.HalfEdge.IsSmooth);
        Assert.False(top.HalfEdge.IsSoft);

        undo.Redo();
        Assert.True(top.HalfEdge.IsSmooth);
        Assert.True(top.HalfEdge.IsSoft);
    }

    [Fact]
    public void Tool_BoundaryEdges_NotSmoothed()
    {
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(10, 0, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(10, 10, 0));
        MeshVertex d = mesh.AddVertex(new Vector3d(0, 10, 0));
        MeshFace face = mesh.AddFace([a, b, c, d]);
        (Scene scene, _) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(5, -40, 25), new Vector3d(5, 5, 10));
        var tool = new SoftenSmoothTool();
        tool.Activate(context);
        tool.TryApplyValue(120);

        Vector2d pixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(5, 5, 0));
        tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));
        tool.OnPointer(PointerInput.Release(pixel, PointerButton.Left));

        // у рёбер нет пары граней — угол не определён, флаги не меняются
        Assert.False(face.HalfEdge.IsSmooth);
        Assert.False(undo.CanUndo);
    }
}
