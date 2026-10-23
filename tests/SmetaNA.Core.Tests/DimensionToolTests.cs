using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Инструмент размера: выбор точек/ребра, размещение размерной линии,
/// точный ввод VCB, undo/redo и отмена.
/// </summary>
public class DimensionToolTests
{
    private static (ToolContext Context, UndoStack Undo, Scene Scene) Setup(Scene scene, InferenceViewport? viewport = null)
    {
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = viewport ?? ToolsTestSupport.ViewportFrom(new Vector3d(50, -100, 200), new Vector3d(50, 0, 0));
        return (context, undo, scene);
    }

    /// <summary>Решатель привязок с очередью точек (по одной на каждый запрос).</summary>
    private static void QueueSnaps(ToolContext context, params Vector3d[] points)
    {
        var queue = new Queue<SnapCandidate>(points.Select(p =>
            new SnapCandidate(SnapType.Endpoint, p, 1, 10, SnapMarkerKind.Cube)));
        context.SnapSolver = _ => new SnapResult(queue.Count > 0 ? [queue.Dequeue()] : []);
    }

    [Fact]
    public void ClickClickClick_CreatesDimension_WithUndoRedo()
    {
        var scene = new Scene();
        (ToolContext context, UndoStack undo, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0), new Vector3d(50, 30, 0));
        var tool = new DimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        Assert.Equal(DimensionToolState.StartPicked, tool.State);
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.Equal(DimensionToolState.EndPicked, tool.State);
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 40), PointerButton.Left));

        var dimension = Assert.IsType<DimensionNode>(Assert.Single(scene.Root.Children));
        Assert.Equal(100, dimension.ValueMm, 6);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(0, 0, 0), dimension.Start));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(100, 0, 0), dimension.End));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(0, 30, 0), dimension.LineOffset, 1e-6));
        Assert.Equal(DimensionToolState.Idle, tool.State);

        undo.Undo();
        Assert.Empty(scene.Root.Children);
        undo.Redo();
        Assert.Equal(100, Assert.IsType<DimensionNode>(Assert.Single(scene.Root.Children)).ValueMm, 6);
    }

    [Fact]
    public void VcbValue_AfterFirstClick_SetsExactEndThenOffset()
    {
        var scene = new Scene();
        (ToolContext context, UndoStack undo, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0));
        var tool = new DimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        Assert.True(tool.TryApplyValue(120));
        Assert.Equal(DimensionToolState.EndPicked, tool.State);
        Assert.Equal(120, (tool.End - tool.Start).Length(), 9);

        Assert.True(tool.TryApplyValue(25));
        var dimension = Assert.IsType<DimensionNode>(Assert.Single(scene.Root.Children));
        Assert.Equal(120, dimension.ValueMm, 6);
        Assert.Equal(25, dimension.LineOffset.Length(), 6);
        Assert.Contains("Размер", undo.NextUndoDescription);
    }

    [Fact]
    public void VcbValue_IsReportedAsLengthDuringConstruction()
    {
        var scene = new Scene();
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(60, 0, 0), new Vector3d(60, 0, 0), new Vector3d(60, 30, 0));
        var tool = new DimensionTool();
        tool.Activate(context);

        Assert.False(tool.Vcb.IsActive);
        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        Assert.True(tool.Vcb.IsActive);
        Assert.Equal(ToolValueKind.Length, tool.Vcb.Kind);

        tool.OnPointer(PointerInput.Move(new Vector2d(10, 20)));
        Assert.NotNull(tool.Vcb.Value);

        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.True(tool.Vcb.IsActive);

        tool.OnPointer(PointerInput.Press(new Vector2d(20, 40), PointerButton.Left));
        Assert.False(tool.Vcb.IsActive);
    }

    [Fact]
    public void ClickOnEdge_WithoutPointSnap_CreatesEdgeDimension()
    {
        HalfEdgeMesh box = ToolsTestSupport.Box(10, 20, 30);
        (Scene scene, _) = ToolsTestSupport.SceneWith(box);
        var viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(5, 10, 500), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), 120, 800, 600);
        (ToolContext context, _, _) = Setup(scene, viewport);
        context.SnapSolver = _ => SnapResult.Empty;

        Vector2d midEdgePx = ToolsTestSupport.PixelOf(viewport, new Vector3d(5, 0, 0));
        var tool = new DimensionTool();
        tool.Activate(context);
        tool.OnPointer(PointerInput.Press(midEdgePx, PointerButton.Left));

        // Клик по ребру без точечной привязки сразу задаёт измеряемый отрезок.
        Assert.Equal(DimensionToolState.EndPicked, tool.State);
        Assert.Equal(10, (tool.End - tool.Start).Length(), 6);
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Enter)));

        var dimension = Assert.Single(scene.Root.Children.OfType<DimensionNode>());
        Assert.Equal(10, dimension.ValueMm, 6);

        // Взгляд вдоль -Z: ребра X на y = 0 (z = 0 и z = 30) проецируются совпадающе —
        // инструмент берёт ближайшее к камере, но длина ребра всегда 10 мм.
        Vector3d delta = dimension.End - dimension.Start;
        Assert.Equal(10, Math.Abs(delta.X), 6);
        Assert.Equal(0, delta.Y, 6);
        Assert.Equal(0, delta.Z, 6);
        Assert.Equal(0, dimension.Start.Y, 6);
        Assert.Contains(dimension.Start.Z, new[] { 0.0, 30.0 });
    }

    [Fact]
    public void Escape_StepsBackThenResets()
    {
        var scene = new Scene();
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new DimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.Equal(DimensionToolState.EndPicked, tool.State);

        Assert.True(tool.OnKey(new KeyInput(ToolKey.Escape)));
        Assert.Equal(DimensionToolState.StartPicked, tool.State);
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Escape)));
        Assert.Equal(DimensionToolState.Idle, tool.State);
        Assert.False(tool.OnKey(new KeyInput(ToolKey.Escape)));
        Assert.Empty(scene.Root.Children);
    }

    [Fact]
    public void Enter_AtPlacement_CommitsDimension()
    {
        var scene = new Scene();
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new DimensionTool();
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        Assert.True(tool.OnKey(new KeyInput(ToolKey.Enter)));

        Assert.Equal(100, Assert.IsType<DimensionNode>(Assert.Single(scene.Root.Children)).ValueMm, 6);
    }

    [Fact]
    public void Cancel_ClearsInteraction_WithoutSceneChanges()
    {
        var scene = new Scene();
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0));
        var tool = new DimensionTool();
        tool.Activate(context);
        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));

        tool.Cancel();

        Assert.Equal(DimensionToolState.Idle, tool.State);
        Assert.Empty(scene.Root.Children);
        Assert.False(tool.Vcb.IsActive);
        Assert.True(tool.Preview.IsEmpty);
    }

    [Fact]
    public void LinearKind_MeasuresProjectedLength()
    {
        var scene = new Scene();
        (ToolContext context, _, _) = Setup(scene);
        QueueSnaps(context, new Vector3d(0, 0, 0), new Vector3d(100, 0, 0));
        var tool = new DimensionTool { Kind = DimensionKind.Linear };
        tool.Activate(context);

        tool.OnPointer(PointerInput.Press(new Vector2d(10, 10), PointerButton.Left));
        tool.OnPointer(PointerInput.Press(new Vector2d(20, 10), PointerButton.Left));
        tool.OnKey(new KeyInput(ToolKey.Enter));

        var dimension = Assert.IsType<DimensionNode>(Assert.Single(scene.Root.Children));
        Assert.Equal(DimensionKind.Linear, dimension.Kind);
        Assert.NotNull(dimension.MeasureDirection);
        Assert.Equal(100, dimension.ValueMm, 6);
    }
}
