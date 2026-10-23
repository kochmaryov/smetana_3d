using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Масштабирование модели по заданному размеру (задача 3.2): точность ≤ 0,01 мм
/// (DoD §5.4), сохранение пропорций, неподвижная первая точка, undo/redo.
/// </summary>
public class ScaleToDimensionTests
{
    private static (ToolContext Context, UndoStack Undo, Scene Scene) Setup(Scene scene)
    {
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = ToolsTestSupport.ViewportFrom(new Vector3d(50, -100, 200), new Vector3d(50, 0, 0));
        return (context, undo, scene);
    }

    private static void QueueSnaps(ToolContext context, params Vector3d[] points)
    {
        var queue = new Queue<SnapCandidate>(points.Select(p =>
            new SnapCandidate(SnapType.Endpoint, p, 1, 10, SnapMarkerKind.Cube)));
        context.SnapSolver = _ => new SnapResult(queue.Count > 0 ? [queue.Dequeue()] : []);
    }

    /// <summary>Мировая трансформация узла (произведение локальных трансформаций предков).</summary>
    private static Transform3d WorldTransformOf(SceneNode node)
    {
        Transform3d world = node.LocalTransform;
        for (SceneNode? parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            world = Transform3d.Multiply(world, parent.LocalTransform);
        }

        return world;
    }

    /// <summary>Мировые габариты меш-узла (с учётом трансформаций).</summary>
    private static BoundingBox3d WorldBounds(MeshNode node)
        => BoundingBox3d.FromMesh(node.Mesh).Transformed(WorldTransformOf(node));

    /// <summary>Размещения всех мешей сцены (мировые трансформации).</summary>
    private static IReadOnlyList<PlacedNode> MeshPlacements(Scene scene)
    {
        var result = new List<PlacedNode>();
        foreach (PlacedNode placed in SceneFlattener.Expand(scene))
        {
            if (placed.Node is MeshNode)
            {
                result.Add(placed);
            }
        }

        return result;
    }

    [Fact]
    public void TargetValue_RescalesModel_WithinDodTolerance_PreservingProportions()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(100, 50, 25);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, UndoStack undo, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.Equal(ScaleToDimensionToolState.AwaitingValue, tool.State);
        Assert.True(tool.TryApplyValue(250));

        BoundingBox3d bounds = WorldBounds(node);
        double width = bounds.Size.X;
        Assert.True(Math.Abs(width - 250) <= DimensionMath.ToleranceMm, $"погрешность {Math.Abs(width - 250)} мм");

        // Пропорции сохранены: Y и Z масштабируются тем же множителем (×2.5).
        Assert.True(Math.Abs(bounds.Size.Y - 125) <= DimensionMath.ToleranceMm);
        Assert.True(Math.Abs(bounds.Size.Z - 62.5) <= DimensionMath.ToleranceMm);
        Assert.Equal(100.0 / 50, bounds.Size.X / bounds.Size.Y, 9);

        // Первая точка — неподвижная точка масштаба.
        Assert.True(ToolsTestSupport.NearlyEqual(Vector3d.Zero, bounds.Min, 1e-6));

        undo.Undo();
        Assert.True(Math.Abs(WorldBounds(node).Size.X - 100) <= DimensionMath.ToleranceMm);

        undo.Redo();
        Assert.True(Math.Abs(WorldBounds(node).Size.X - 250) <= DimensionMath.ToleranceMm);
    }

    [Fact]
    public void VcbValue_ShrinksModel()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(100, 50, 25);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.True(tool.TryApplyValue(80));

        Assert.True(Math.Abs(WorldBounds(node).Size.X - 80) <= DimensionMath.ToleranceMm);
    }

    [Fact]
    public void VcbValue_AfterFirstClick_SetsExactMeasuredLength()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(100, 50, 25);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        Assert.True(tool.TryApplyValue(75));
        Assert.Equal(ScaleToDimensionToolState.AwaitingValue, tool.State);
        Assert.Equal(75, tool.CurrentLengthMm, 9);

        // Текущий размер 75 → целевой 150: ×2.
        Assert.True(tool.TryApplyValue(150));
        Assert.Equal(ScaleToDimensionToolState.Idle, tool.State);
    }

    [Fact]
    public void InvalidTargetValue_IsRejected()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(100, 50, 25);
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));

        Assert.False(tool.TryApplyValue(0));
        Assert.False(tool.TryApplyValue(-10));
        Assert.False(tool.TryApplyValue(double.NaN));
        Assert.Equal(ScaleToDimensionToolState.AwaitingValue, tool.State);
        Assert.True(Math.Abs(WorldBounds(node).Size.X - 100) <= DimensionMath.ToleranceMm);
    }

    [Fact]
    public void Escape_StepsBackThenResets()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(100, 50, 25);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Escape)));
        Assert.Equal(ScaleToDimensionToolState.StartPicked, tool.State);
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Escape)));
        Assert.Equal(ScaleToDimensionToolState.Idle, tool.State);
    }

    [Fact]
    public void ClickOnEdge_WithoutPointSnap_MeasuresWholeEdge()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 20, 30);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        var viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(5, 10, 500), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), 120, 800, 600);
        (ToolContext context, _, _) = Setup(scene);
        context.Viewport = viewport;
        context.SnapSolver = _ => SnapResult.Empty;

        Vector2d midEdgePx = ToolsTestSupport.PixelOf(viewport, new Vector3d(5, 0, 0));
        var tool = new ScaleToDimensionTool();
        tool.Activate(context);
        tool.OnPointer(PointerInput.Press(midEdgePx, PointerButton.Left));

        Assert.Equal(ScaleToDimensionToolState.AwaitingValue, tool.State);
        Assert.Equal(10, tool.CurrentLengthMm, 6);
    }

    [Fact]
    public void ScaleModelCommand_SharedComponentContent_ScalesAllInstancesConsistently()
    {
        var scene = new Scene();
        var content = new GroupNode();
        content.AddChild(new MeshNode { Mesh = ToolsTestSupport.Box(10, 10, 10) });
        var definition = new ComponentDefinition { Content = content };
        var first = new ComponentInstanceNode { Definition = definition };
        var second = new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(100, 0, 0)),
        };
        scene.Root.AddChild(first);
        scene.Root.AddChild(second);

        IReadOnlyList<PlacedNode> before = MeshPlacements(scene);
        Assert.Equal(2, before.Count);

        var command = new ScaleModelCommand(scene, Vector3d.Zero, 2);
        command.Execute();

        IReadOnlyList<PlacedNode> after = MeshPlacements(scene);
        Assert.Equal(before.Count, after.Count);
        for (int i = 0; i < before.Count; i++)
        {
            BoundingBox3d was = BoundingBox3d.FromMesh(((MeshNode)before[i].Node).Mesh).Transformed(before[i].WorldTransform);
            BoundingBox3d now = BoundingBox3d.FromMesh(((MeshNode)after[i].Node).Mesh).Transformed(after[i].WorldTransform);
            Assert.Equal(was.Size * 2, now.Size);
            Assert.Equal(was.Min * 2, now.Min);
        }

        command.Undo();
        IReadOnlyList<PlacedNode> reverted = MeshPlacements(scene);
        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].WorldTransform, reverted[i].WorldTransform);
        }
    }

    [Fact]
    public void ScaleModelCommand_RejectsNonPositiveFactor()
    {
        var scene = new Scene();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaleModelCommand(scene, Vector3d.Zero, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaleModelCommand(scene, Vector3d.Zero, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaleModelCommand(scene, Vector3d.Zero, double.NaN));
    }
}
