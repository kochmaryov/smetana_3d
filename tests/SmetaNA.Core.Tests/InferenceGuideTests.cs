using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Направляющие (линейка/транспортир): модель, запросы, очистка и привязки.</summary>
public sealed class InferenceGuideTests
{
    [Fact]
    public void Guide_NormalizesDirection()
    {
        var guide = new SnapGuide(new Vector3d(1, 2, 3), new Vector3d(0, 0, 4));

        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), guide.Direction);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(double.NaN, 0, 0)]
    public void Guide_InvalidDirection_Throws(double x, double y, double z)
    {
        Assert.Throws<ArgumentException>(() => new SnapGuide(Vector3d.Zero, new Vector3d(x, y, z)));
    }

    [Fact]
    public void GuideSet_AddLineAndAngle_TracksKind()
    {
        var set = new GuideSet();

        SnapGuide line = set.AddLine(Vector3d.Zero, Vector3d.UnitX);
        SnapGuide angle = set.AddAngle(new Vector3d(1, 1, 1), new Vector3d(1, 1, 0));

        Assert.Equal(2, set.Count);
        Assert.Equal(SnapGuideKind.Line, line.Kind);
        Assert.Equal(SnapGuideKind.Angle, angle.Kind);
        Assert.Same(line, set.Find(line.Id));
    }

    [Fact]
    public void GuideSet_RemoveAndClear()
    {
        var set = new GuideSet();
        SnapGuide line = set.AddLine(Vector3d.Zero, Vector3d.UnitX);
        set.AddAngle(new Vector3d(1, 1, 1), new Vector3d(1, 1, 0));

        Assert.True(set.Remove(line.Id));
        Assert.False(set.Remove(line.Id));
        Assert.Equal(1, set.Count);

        set.Clear();
        Assert.Equal(0, set.Count);
        Assert.Empty(set.Guides);
    }

    [Fact]
    public void GuideSet_Intersections_ReturnsCrossingPoint()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(15, 10, 0), Vector3d.UnitX);
        set.AddLine(new Vector3d(15, 10, 0), Vector3d.UnitY);

        IReadOnlyList<Vector3d> intersections = set.Intersections(tolerance: 0.01);

        Assert.Single(intersections);
        InferenceAssert.PointEqual(new Vector3d(15, 10, 0), intersections[0]);
    }

    [Fact]
    public void GuidePoint_OnGuideLine_IsSnapped()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(10, 10, 0), Vector3d.UnitX);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(15, 10, 0), guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Guide, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(15, 10, 0), result.Best.Point);
        Assert.Equal(SnapMarkerKind.Dot, result.Best.Marker);
    }

    [Fact]
    public void GuideIntersections_GuideAndGuide_IsSnapped()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(15, 10, 0), Vector3d.UnitX);
        set.AddLine(new Vector3d(15, 10, 0), Vector3d.UnitY);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(15, 10, 0), guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Intersection, result.Best!.Type);
        InferenceAssert.PointEqual(new Vector3d(15, 10, 0), result.Best.Point);
    }

    [Fact]
    public void GuideIntersections_GuideAndEdge_IsSnapped()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(25, 10, 0), Vector3d.UnitX);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceTestSupport.CrossingEdges(),
            new Vector3d(25, 10, 0),
            guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        SnapCandidate intersection = InferenceAssert.Single(result, SnapType.Intersection);
        InferenceAssert.PointEqual(new Vector3d(25, 10, 0), intersection.Point);
    }

    [Fact]
    public void Parallel_ToGuide_IsSnapped()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(10, 10, 0), new Vector3d(1, 1, 0));
        var anchor = new Vector3d(5, 20, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(15, 30, 0), anchor: anchor, guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Parallel, result.Best!.Type);
        InferenceAssert.DirectionEqual(new Vector3d(1, 1, 0), result.Best.Direction!.Value);
        InferenceAssert.PointEqual(new Vector3d(15, 30, 0), result.Best.Point);
    }

    [Fact]
    public void Perpendicular_ToGuide_IsCombinedThroughGuidePoint()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(15, 10, 0), new Vector3d(1, 1, 0));
        var anchor = new Vector3d(20, 5, 0);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(), InferenceScene.Empty, new Vector3d(15, 10, 0), anchor: anchor, guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        Assert.NotNull(result.Best);
        Assert.Equal(SnapType.Perpendicular, result.Best!.Type);
        Assert.True(result.Best.IsCombined);
        Assert.Equal(SnapDefaults.GuidePriority + 1, result.Best.Priority);
        InferenceAssert.PointEqual(new Vector3d(15, 10, 0), result.Best.Point);
        InferenceAssert.DirectionEqual(new Vector3d(-1, 1, 0), result.Best.Direction!.Value);
    }

    [Fact]
    public void Filter_DisabledGuides_YieldsNoGuideCandidates()
    {
        var set = new GuideSet();
        set.AddLine(new Vector3d(10, 10, 0), Vector3d.UnitX);
        SnapRequest request = InferenceTestSupport.Request(
            InferenceTestSupport.TopDown(),
            InferenceScene.Empty,
            new Vector3d(15, 10, 0),
            filter: SnapFilter.All & ~SnapFilter.Guides,
            guides: set.Guides);

        SnapResult result = InferenceEngine.Solve(request);

        InferenceAssert.None(result, SnapType.Guide);
    }
}
