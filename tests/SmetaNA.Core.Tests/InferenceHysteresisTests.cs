using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// W4: антимерцание привязок (R-31), перебор кандидатов Tab (R-25) и фиксация
/// плоскости построения Shift+Alt (R-23).
/// </summary>
public sealed class InferenceHysteresisTests
{
    private static SnapRequest EdgeRequest() =>
        InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(15, 11.5, 0));

    private static SnapCandidate OnFaceCandidateNearCursor()
    {
        // Кандидат «на грани» чуть ниже курсора: приоритет 400 — всего на 50 ниже
        // «на ребре» (450), то есть новый кандидат не лучше заметно.
        return new SnapCandidate(
            SnapType.OnFace,
            new Vector3d(15, 11.4, 0),
            0.9,
            SnapDefaults.OnFacePriority,
            SnapMarkerKind.Dot)
        {
            ScreenDistancePx = 0.4,
        };
    }

    [Fact]
    public void FreshCandidate_IsHeld_WhenNewBestIsOnlySlightlyBetter()
    {
        SnapResult result = InferenceEngine.Solve(
            EdgeRequest() with { PreviousBest = OnFaceCandidateNearCursor(), PreviousBestAgeMs = 50 });

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.OnFace, result.Best!.Type);
        Assert.Contains(result.Candidates, candidate => candidate.Type == SnapType.OnEdge);
    }

    [Fact]
    public void StaleCandidate_LosesToPriorityWinner()
    {
        SnapResult result = InferenceEngine.Solve(
            EdgeRequest() with { PreviousBest = OnFaceCandidateNearCursor(), PreviousBestAgeMs = 400 });

        Assert.Equal(SnapType.OnEdge, result.Best!.Type);
    }

    [Fact]
    public void FreshCandidate_LosesToClearlyStrongerCandidate()
    {
        SnapRequest baseRequest = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(10.4, 10.4, 0));
        var previous = new SnapCandidate(
            SnapType.OnEdge,
            new Vector3d(11, 10, 0),
            0.9,
            SnapDefaults.OnEdgePriority,
            SnapMarkerKind.Dot)
        {
            ScreenDistancePx = 1.0,
        };

        SnapResult result = InferenceEngine.Solve(
            baseRequest with { PreviousBest = previous, PreviousBestAgeMs = 50 });

        // Вершина (900) заметно приоритетнее ребра (450) — перехват сразу.
        Assert.Equal(SnapType.Endpoint, result.Best!.Type);
    }

    [Fact]
    public void Hysteresis_IsIgnoredWhenPreviousIsFarFromCursor()
    {
        var previous = new SnapCandidate(
            SnapType.OnFace,
            new Vector3d(15, 30, 0),
            0.9,
            SnapDefaults.OnFacePriority,
            SnapMarkerKind.Dot);

        SnapResult result = InferenceEngine.Solve(
            EdgeRequest() with { PreviousBest = previous, PreviousBestAgeMs = 20 });

        Assert.Equal(SnapType.OnEdge, result.Best!.Type);
    }

    [Fact]
    public void WithBest_ReordersCandidates_WithoutLosingThem()
    {
        var first = new SnapCandidate(SnapType.Endpoint, Vector3d.Zero, 1, 900, SnapMarkerKind.Circle);
        var second = new SnapCandidate(SnapType.Midpoint, Vector3d.UnitX, 0.5, 700, SnapMarkerKind.Circle);
        var third = new SnapCandidate(SnapType.OnFace, Vector3d.UnitY, 0.2, 400, SnapMarkerKind.Dot);
        var result = new SnapResult([first, second, third]);

        SnapResult cycled = result.WithBest(1);

        Assert.Same(second, cycled.Best);
        Assert.Equal([second, first, third], cycled.Candidates);
        Assert.Same(first, result.WithBest(3).Best);
        Assert.Same(first, result.WithBest(0).Best);
    }

    [Fact]
    public void ToolContext_CycleAndReset_CandidateOffset()
    {
        Scene scene = new();
        (ToolContext context, _) = ToolsTestSupport.ContextFor(scene);

        Assert.Equal(0, context.SnapCandidateOffset);
        context.CycleSnapCandidate();
        context.CycleSnapCandidate();
        Assert.Equal(2, context.SnapCandidateOffset);

        context.ResetSnapCandidateOffset();
        Assert.Equal(0, context.SnapCandidateOffset);
    }

    [Fact]
    public void ShiftAlt_LocksBuildingPlaneToHoveredFace()
    {
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(ToolsTestSupport.Box(10, 10, 10));
        (ToolContext context, _) = DrawingTestSupport.Setup(scene);
        var tool = new LineTool();
        tool.Activate(context);
        Vector3d onTopFace = new(5, 5, 10);

        // Shift+Alt над верхней гранью фиксирует её плоскость; курсор уходит в сторону,
        // но точка построения остаётся на z = 10.
        DrawingTestSupport.Click(context, tool, onTopFace, ToolModifiers.Shift | ToolModifiers.Alt);

        Assert.Equal(10, tool.Chain[0].Z, 6);
        Assert.NotNull(node);
    }
}
