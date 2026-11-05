using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Аудит §15.4 (P0): координатный ввод VCB в Rotate, Scale, Dimension
/// и «Масштаб по размеру» (R-19).
/// </summary>
public sealed class ToolCoordinateInputTransformTests
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
    public void Rotate_PointInput_RotatesByRightAngle()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        MeshFace top = ToolsTestSupport.FaceWithNormal(mesh, Vector3d.UnitZ);
        IReadOnlyList<MeshVertex> ring = mesh.GetRing(top);
        var before = ring.ToDictionary(vertex => vertex, vertex => vertex.Position);

        var tool = new RotateTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));

        // цель — точка строго по +Y от центра грани (5,5,10): поворот на 90° вокруг Z
        Assert.True(tool.TryApplyPoint(new Vector3d(5, 15, 10), relative: false));

        Assert.False(tool.IsInteracting);
        Assert.True(undo.CanUndo);
        Assert.Equal(90, Math.Abs(tool.AngleDegrees), 3);

        // кольцо повернулось жёстко: длины сторон сохранены
        for (int i = 0; i < ring.Count; i++)
        {
            MeshVertex current = ring[i];
            MeshVertex next = ring[(i + 1) % ring.Count];
            Assert.Equal(
                (before[current] - before[next]).Length(),
                (current.Position - next.Position).Length(),
                6);
        }

        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void Scale_PointInput_DoublesSize()
    {
        (ToolContext context, UndoStack undo) = Setup(out HalfEdgeMesh mesh);
        var tool = new ScaleTool();
        tool.Activate(context);

        Vector2d pressPixel = ToolsTestSupport.PixelOf(context.Viewport!, new Vector3d(2, 5, 10));
        Assert.True(tool.OnPointer(PointerInput.Press(pressPixel, PointerButton.Left)));

        // опорный радиус от центра (5,5,10) — 3 мм; целевая точка в 6 мм → ×2
        Assert.True(tool.TryApplyPoint(new Vector3d(11, 5, 10), relative: false));

        Assert.False(tool.IsInteracting);
        Assert.Equal(2, tool.Factor, 6);
        Assert.True(undo.CanUndo);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void Rotate_PointInputBeforeDrag_IsNotApplicable()
    {
        (ToolContext context, _) = Setup(out _);
        var tool = new RotateTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyPoint(new Vector3d(5, 15, 10), relative: false));
    }

    [Fact]
    public void Dimension_PointInput_CommitsAnnotation()
    {
        var scene = new Scene();
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);
        var tool = new DimensionTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.Equal(DimensionToolState.StartPicked, tool.State);
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 0, 0), relative: true));

        Assert.Equal(DimensionToolState.EndPicked, tool.State);
        Assert.Equal(new Vector3d(10, 0, 0), tool.End);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 5, 0), relative: false));
        Assert.Equal(DimensionToolState.Idle, tool.State);

        DimensionNode node = Assert.Single(scene.Root.Children.OfType<DimensionNode>());
        Assert.Equal(10, node.ValueMm, 6);
    }

    [Fact]
    public void Dimension_RelativeStart_IsRejected()
    {
        var scene = new Scene();
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);
        var tool = new DimensionTool();
        tool.Activate(context);

        Assert.False(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: true));
        Assert.Equal(DimensionToolState.Idle, tool.State);
    }

    [Fact]
    public void ScaleToDimension_PointInputThenValue_ScalesModel()
    {
        var scene = new Scene();
        var node = new MeshNode { Mesh = ToolsTestSupport.Box(10, 10, 10) };
        scene.Root.AddChild(node);
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        Assert.True(tool.TryApplyPoint(new Vector3d(0, 0, 0), relative: false));
        Assert.True(tool.TryApplyPoint(new Vector3d(10, 0, 0), relative: true));
        Assert.Equal(ScaleToDimensionToolState.AwaitingValue, tool.State);
        Assert.Equal(10, tool.CurrentLengthMm, 6);

        Assert.True(tool.TryApplyValue(20));
        Assert.Equal(ScaleToDimensionToolState.Idle, tool.State);

        // команда масштабирует трансформации узлов: вершина (10,0,0) уходит в 20
        Vector3d scaled = node.WorldTransform().TransformPoint(new Vector3d(10, 0, 0));
        Assert.Equal(20, scaled.X, 6);
    }
}
