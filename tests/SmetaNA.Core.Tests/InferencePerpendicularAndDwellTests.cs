using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Аудит §15.4: перпендикуляр к грани (R-33), касательная в вершине дуги (R-34)
/// и приоритизация удержанием (R-37).
/// </summary>
public sealed class InferencePerpendicularAndDwellTests
{
    [Fact]
    public void PerpendicularToFace_FollowsHoveredFaceNormal()
    {
        InferenceViewport viewport = InferenceTestSupport.Oblique();
        SnapRequest request = InferenceTestSupport.Request(
            viewport,
            InferenceTestSupport.Cube(),
            new Vector3d(10, 10, 20),
            anchor: new Vector3d(10, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate candidate = InferenceAssert.Single(result, SnapType.PerpendicularToFace);
        Assert.NotNull(candidate.Direction);
        Assert.Equal(1, Math.Abs(Vector3d.Dot(candidate.Direction!.Value, Vector3d.UnitZ)), 6);
        Assert.Equal(SnapDefaults.PerpendicularToFacePriority, candidate.Priority);
    }

    [Fact]
    public void PerpendicularToFace_WithoutAnchor_IsNotProduced()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.Oblique(),
            InferenceTestSupport.Cube(),
            new Vector3d(10, 10, 20));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.PerpendicularToFace);
    }

    [Fact]
    public void DwellBoost_RaisesHeldCandidateAboveCloserPriority()
    {
        // Курсор над ребром: лучший кандидат — OnEdge (450), а удержанная ранее
        // точка на грани (400) с надбавкой +50 обгоняет его.
        InferenceViewport viewport = InferenceTestSupport.TopDown();
        SnapRequest request = InferenceTestSupport.Request(
            viewport,
            SceneWithFaceAndEdge(),
            new Vector3d(23, 10.4, 0));

        SnapCandidate onFace = InferenceAssert.Single(InferenceEngine.Solve(request), SnapType.OnFace);
        Assert.Equal(SnapDefaults.OnFacePriority, onFace.Priority);

        SnapResult dwelled = InferenceEngine.Solve(request with
        {
            PreviousBest = onFace,
            PreviousBestAgeMs = SnapDefaults.DwellPriorityMs + 50,
        });

        Assert.Equal(SnapType.OnFace, dwelled.Best!.Type);
        Assert.Equal(SnapDefaults.OnFacePriority + SnapDefaults.DwellPriorityBonus, dwelled.Best.Priority);
    }

    [Fact]
    public void DwellBoost_RequiresHeldLongEnough()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();
        SnapRequest request = InferenceTestSupport.Request(
            viewport,
            SceneWithFaceAndEdge(),
            new Vector3d(23, 10.4, 0));

        // 200 мс: гистерезис (150 мс) уже не действует, а порог удержания (250 мс) не достигнут.
        SnapCandidate onFace = InferenceAssert.Single(InferenceEngine.Solve(request), SnapType.OnFace);
        SnapResult fresh = InferenceEngine.Solve(request with
        {
            PreviousBest = onFace,
            PreviousBestAgeMs = (int)SnapDefaults.DwellMs + 50,
        });

        Assert.Equal(SnapType.OnEdge, fresh.Best!.Type);
        Assert.Equal(SnapDefaults.OnEdgePriority, fresh.Best.Priority);
    }

    [Fact]
    public void TangentAtVertex_FollowsArcTangentDirection()
    {
        var scene = new InferenceScene(
            [],
            [],
            arcTangents:
            [
                new SnapArcTangent(
                    new Vector3d(20, 0, 0),
                    Vector3d.UnitY,
                    new SnapEntityRef(Guid.NewGuid(), SnapEntityKind.Circle, 0, "Arc")),
            ]);

        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            scene,
            new Vector3d(20, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate candidate = InferenceAssert.Single(result, SnapType.TangentAtVertex);
        Assert.NotNull(candidate.Direction);
        InferenceAssert.DirectionEqual(Vector3d.UnitY, candidate.Direction!.Value);
        Assert.Equal(1, Math.Abs(Vector3d.Dot(candidate.Direction.Value, Vector3d.UnitY)), 6);
        InferenceAssert.PointEqual(new Vector3d(20, 10, 0), candidate.Point, 1e-3);
    }

    private static InferenceScene SceneWithFaceAndEdge() =>
        new(
            [new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 10, 0))],
            [
                new SnapFace([
                    new Vector3d(0, 0, 0), new Vector3d(30, 0, 0), new Vector3d(30, 30, 0), new Vector3d(0, 30, 0),
                ]),
            ]);
}
