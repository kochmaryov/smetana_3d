using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Точечные привязки движка: куб, рёбра, приоритеты и толерансы.</summary>
public sealed class InferenceEnginePointSnapTests
{
    [Theory]
    [InlineData(0, 0, 20)]
    [InlineData(20, 0, 20)]
    [InlineData(20, 20, 20)]
    [InlineData(0, 20, 20)]
    [InlineData(0, 0, 0)]
    [InlineData(20, 0, 0)]
    [InlineData(20, 20, 0)]
    [InlineData(0, 20, 0)]
    public void Endpoint_OnCubeCorner_BestIsEndpointAtCorner(double x, double y, double z)
    {
        var corner = new Vector3d(x, y, z);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.Oblique(), InferenceTestSupport.Cube(), corner);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Endpoint, result.Best!.Type);
        InferenceAssert.PointEqual(corner, result.Best.Point);
        Assert.Equal(SnapMarkerKind.Cube, result.Best.Marker);
        Assert.Equal(1.0, result.Best.Confidence, 6);
    }

    [Fact]
    public void Endpoint_OnTopViewCorner_NearerVertexComesFirst()
    {
        // На виде сверху вершины (0, 0, 0) и (0, 0, 20) проецируются в один пиксель.
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.Cube(), new Vector3d(0, 0, 10));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Endpoint, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(0, 0, 20), result.Best.Point);
    }

    [Theory]
    [InlineData(15, 10, 0, SnapType.Endpoint, 10, 10, 0)]
    [InlineData(16.5, 10, 0, SnapType.Midpoint, 20, 10, 0)]
    public void Priority_ChoosesByTypeNotOnlyByDistance(
        double cursorX, double cursorY, double cursorZ,
        SnapType expectedType,
        double expectedX, double expectedY, double expectedZ)
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(cursorX, cursorY, cursorZ),
            tolerancePx: 24);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(expectedType, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(expectedX, expectedY, expectedZ), result.Best.Point);
    }

    [Fact]
    public void Priority_EndpointBeatsCloserMidpoint()
    {
        // Курсор ближе к середине (18 px), но конец ребра ещё в толерансе (22 px):
        // приоритет endpoint выше midpoint.
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(15.5, 10, 0),
            tolerancePx: 24);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Endpoint, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(10, 10, 0), result.Best.Point);

        SnapCandidate midpoint = InferenceAssert.Single(result, SnapType.Midpoint);
        List<SnapCandidate> candidates = result.Candidates.ToList();
        Assert.True(
            candidates.IndexOf(result.Best!) < candidates.IndexOf(midpoint),
            "Midpoint должен идти после endpoint в упорядоченном списке.");
    }

    [Fact]
    public void Priority_Defaults_AreOrdered()
    {
        Assert.True(SnapDefaults.PriorityOf(SnapType.Endpoint) > SnapDefaults.PriorityOf(SnapType.Midpoint));
        Assert.True(SnapDefaults.PriorityOf(SnapType.Midpoint) > SnapDefaults.PriorityOf(SnapType.Center));
        Assert.True(SnapDefaults.PriorityOf(SnapType.Intersection) > SnapDefaults.PriorityOf(SnapType.OnFace));
        Assert.True(SnapDefaults.PriorityOf(SnapType.OnFace) > SnapDefaults.PriorityOf(SnapType.AxisX));
    }

    [Fact]
    public void Tolerance_WithinRadius_SnapsEndpoint()
    {
        // 1 единица = 4 px; толеранс 8 px = 2 единицы.
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.SingleEdge(), new Vector3d(11, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.Single(result, SnapType.Endpoint);
    }

    [Fact]
    public void Tolerance_OutsideRadius_YieldsEmpty()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.SingleEdge(), new Vector3d(13, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.True(result.IsEmpty);
        Assert.Null(result.Best);
    }

    [Fact]
    public void Midpoint_OnEdge_SnapsToMidpoint()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.SingleEdge(), new Vector3d(20, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Midpoint, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(20, 10, 0), result.Best.Point);
        Assert.Equal(SnapMarkerKind.Circle, result.Best.Marker);
    }

    [Fact]
    public void Origin_AtWorldOrigin_IsSnapped()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, Vector3d.Zero);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Origin, result.Best!.Type);
        InferenceAssert.PointEqual(Vector3d.Zero, result.Best.Point);
        Assert.Equal(SnapMarkerKind.Ring, result.Best.Marker);
    }

    [Fact]
    public void Center_OnFaceCentroid_BeatsOnFace()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.Cube(), new Vector3d(10, 10, 20));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Center, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(10, 10, 20), result.Best.Point);
        InferenceAssert.Single(result, SnapType.OnFace);
    }

    [Fact]
    public void Center_OnCircleCenter_IsSnapped()
    {
        var scene = new InferenceScene([], [], [new SnapCircle(new Vector3d(10, 10, 0), Vector3d.UnitZ, 5)]);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), scene, new Vector3d(10, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Center, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(10, 10, 0), result.Best.Point);
    }

    [Fact]
    public void OnFace_OnCubeTop_SnapsToNearestFace()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.Cube(), new Vector3d(5, 7, 20));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.OnFace, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(5, 7, 20), result.Best.Point);

        // Луч проходит и нижнюю грань: она идёт после верхней (глубина).
        SnapCandidate[] onFace = [.. result.Candidates.Where(c => c.Type == SnapType.OnFace)];
        Assert.Equal(2, onFace.Length);
        InferenceAssert.PointEqual(new Vector3d(5, 7, 20), onFace[0].Point);
        InferenceAssert.PointEqual(new Vector3d(6.25, 8.75, 0), onFace[1].Point);
    }

    [Fact]
    public void Intersection_CrossingEdges_IsSnapped()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceTestSupport.CrossingEdges(), new Vector3d(25, 10, 0));

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Intersection, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(25, 10, 0), result.Best.Point);
        Assert.Equal(SnapMarkerKind.Cross, result.Best.Marker);
    }

    [Fact]
    public void Intersection_AdjacentCubeEdges_ProducesNoIntersectionAtCorner()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.Oblique(), InferenceTestSupport.Cube(), new Vector3d(20, 20, 20));

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Intersection);
    }

    [Fact]
    public void Filter_DisabledEndpoints_YieldsNoEndpoint()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(11, 10, 0),
            filter: SnapFilter.All & ~SnapFilter.Endpoints);

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Endpoint);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Filter_PointsOnly_YieldsNoLinearCandidates()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(11, 10, 0),
            modifiers: SnapModifiers.PointsOnly);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.All(result.Candidates, c => Assert.Null(c.Direction));
    }

    [Fact]
    public void Solve_InvalidTolerance_Throws()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, Vector3d.Zero, tolerancePx: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => InferenceEngine.Solve(request));
    }

    [Fact]
    public void Candidates_AreOrderedByPriorityThenConfidence()
    {
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.SingleEdge(),
            new Vector3d(15.5, 10, 0),
            tolerancePx: 24);

        SnapResult result = InferenceEngine.Solve(request);

        for (int i = 1; i < result.Candidates.Count; i++)
        {
            SnapCandidate previous = result.Candidates[i - 1];
            SnapCandidate current = result.Candidates[i];
            Assert.True(
                previous.Priority > current.Priority
                || (previous.Priority == current.Priority && previous.Confidence >= current.Confidence),
                "Кандидаты должны быть упорядочены по приоритету и уверенности.");
        }
    }
}
