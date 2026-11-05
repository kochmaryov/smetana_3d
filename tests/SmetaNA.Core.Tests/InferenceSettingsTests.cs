using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W4: настройки привязок, сетка, скрытая геометрия, привязка длины и сохранение
/// составного снимка сцены (середины дуг, начала координат контейнеров).
/// </summary>
public sealed class InferenceSettingsTests
{
    [Fact]
    public void Normalize_ClampsToleranceAndRestoresStep()
    {
        InferenceSettings normalized = new InferenceSettings
        {
            TolerancePx = 500,
            GridStep = -5,
        }.Normalize();

        Assert.Equal(40.0, normalized.TolerancePx);
        Assert.Equal(InferenceSettings.DefaultGridStep, normalized.GridStep);

        InferenceSettings tooSmall = new InferenceSettings { TolerancePx = 0.1 }.Normalize();
        Assert.Equal(2.0, tooSmall.TolerancePx);
    }

    [Fact]
    public void WithExtra_KeepsArcMidpointsAndInstanceOrigins()
    {
        var arcPoint = new SnapArcPoint(new Vector3d(5, 5, 0), SnapEntityRef.None);
        var origin = new SnapInstanceOrigin(
            new Vector3d(1, 2, 3),
            Vector3d.UnitX,
            Vector3d.UnitY,
            Vector3d.UnitZ,
            new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.ComponentInstance, null, "Стол"));
        var scene = new InferenceScene(
            [new SnapSegment(Vector3d.Zero, new Vector3d(10, 0, 0))],
            [],
            [new SnapCircle(new Vector3d(5, 5, 0), Vector3d.UnitZ, 5)],
            [arcPoint],
            [origin]);

        InferenceScene extended = scene.WithExtra(
            extraSegments: [new SnapSegment(new Vector3d(0, 1, 0), new Vector3d(0, 9, 0))]);

        Assert.Equal(2, extended.Segments.Count);
        Assert.Single(extended.Circles);
        Assert.Equal(arcPoint, Assert.Single(extended.ArcPoints));
        Assert.Equal("Стол", Assert.Single(extended.InstanceOrigins).Entity.DisplayName);
    }

    [Fact]
    public void SnapScene_SkipsHiddenNodeUntilEnabled()
    {
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(ToolsTestSupport.Box(10, 10, 10));
        node.IsVisible = false;
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);

        Assert.Empty(context.SnapScene.Segments);

        context.Inference = context.Inference with { ShowHiddenGeometry = true };
        Assert.NotEmpty(context.SnapScene.Segments);

        context.Inference = context.Inference with { ShowHiddenGeometry = false };
        Assert.Empty(context.SnapScene.Segments);

        context.TemporaryIncludeHiddenGeometry = true;
        Assert.NotEmpty(context.SnapScene.Segments);
    }

    [Fact]
    public void BuildSnapRequest_WithGridEnabled_CarriesGrid()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        context.Inference = context.Inference with { GridEnabled = true, GridStep = 10 };

        SnapRequest? request = context.BuildSnapRequest(
            DrawingTestSupport.Px(context, new Vector3d(11, 9, 0)),
            anchor: null,
            ToolModifiers.None,
            grid: context.GridFor(DrawingPlane.Ground));

        Assert.NotNull(request);
        Assert.NotNull(request!.Grid);
        SnapResult result = context.Snap(request);
        Assert.Equal(SnapType.Grid, result.Best?.Type);
    }

    [Fact]
    public void LineTool_WithGridSnapping_SnapsToGridNode()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        context.Inference = context.Inference with { GridEnabled = true, GridStep = 10 };
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(11, 9, 0));

        Assert.NotNull(tool.CurrentSnap.Best);
        Assert.Equal(SnapType.Grid, tool.CurrentSnap.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(10, 10, 0), tool.CurrentSnap.Best.Point);
    }

    [Fact]
    public void LineTool_WithoutGridSnapping_KeepsFreePoint()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(11, 9, 0));

        Assert.NotEqual(SnapType.Grid, tool.CurrentSnap.Best?.Type);
    }

    [Fact]
    public void LineTool_WithLengthSnapping_RoundsSegmentLength()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        context.Inference = context.Inference with { LengthSnappingEnabled = true, LengthPrecision = 0.5 };
        var tool = new LineTool();
        tool.Activate(context);

        DrawingTestSupport.Click(context, tool, new Vector3d(0, 0, 0));
        DrawingTestSupport.Click(context, tool, new Vector3d(10.3, 0, 0));

        Assert.Equal(2, tool.Chain.Count);
        Assert.Equal(10.5, tool.Chain[1].X, 6);
    }

    /// <summary>Тестовый доступ к привязке длины: инструмент-зонд без UI.</summary>
    private sealed class LengthProbeTool : DrawingToolBase
    {
        public override string Id => "test.length";

        public override bool OnPointer(PointerInput input) => false;

        public override void Cancel()
        {
        }

        public override bool TryApplyValue(double value) => false;

        public Vector3d SnapLength(Vector3d point, Vector3d anchor) => ApplyLengthSnapping(point, anchor);

        public static bool IsExact(SnapType type) => IsExactSnap(type);
    }

    [Fact]
    public void LengthSnapping_RoundsDistanceFromAnchor_KeepingDirection()
    {
        Scene scene = new();
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        context.Inference = context.Inference with { LengthSnappingEnabled = true, LengthPrecision = 0.5 };
        var tool = new LengthProbeTool();
        tool.Activate(context);

        Vector3d snapped = tool.SnapLength(new Vector3d(10.3, 0, 0), new Vector3d(0, 0, 0));

        Assert.Equal(10.5, snapped.X, 6);
        Assert.Equal(0, snapped.Y, 6);

        context.Inference = context.Inference with { LengthSnappingEnabled = false };
        Assert.Equal(10.3, tool.SnapLength(new Vector3d(10.3, 0, 0), new Vector3d(0, 0, 0)).X, 6);
    }

    [Fact]
    public void LengthSnapping_SkipsExactSnapTypes()
    {
        Assert.True(LengthProbeTool.IsExact(SnapType.Endpoint));
        Assert.True(LengthProbeTool.IsExact(SnapType.Midpoint));
        Assert.True(LengthProbeTool.IsExact(SnapType.Intersection));
        Assert.True(LengthProbeTool.IsExact(SnapType.Grid));
        Assert.False(LengthProbeTool.IsExact(SnapType.OnFace));
        Assert.False(LengthProbeTool.IsExact(SnapType.OnEdge));
        Assert.False(LengthProbeTool.IsExact(SnapType.AxisX));
    }

    [Fact]
    public void ResolveSnapModifiers_CtrlShift_IsHiddenGeometryNotPointsOnly()
    {
        Scene scene = new();
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);
        context.Inference = context.Inference with { ShowHiddenGeometry = false };

        SnapModifiers withCtrl = context.ResolveSnapModifiers(ToolModifiers.Ctrl);
        Assert.True((withCtrl & SnapModifiers.PointsOnly) != 0);

        SnapModifiers withCtrlShift = context.ResolveSnapModifiers(ToolModifiers.Ctrl | ToolModifiers.Shift);
        Assert.False((withCtrlShift & SnapModifiers.PointsOnly) != 0);
    }
}
