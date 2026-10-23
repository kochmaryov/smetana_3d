using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Валидация моделей inference: луч, снимок, кандидаты, запрос.</summary>
public sealed class InferenceModelTests
{
    [Fact]
    public void Ray3d_NormalizesDirection()
    {
        var ray = new Ray3d(new Vector3d(1, 2, 3), new Vector3d(0, 0, 5));

        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), ray.Direction);
        InferenceAssert.PointEqual(new Vector3d(1, 2, 8), ray.PointAt(5));
    }

    [Fact]
    public void Ray3d_ZeroDirection_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Ray3d(Vector3d.Zero, Vector3d.Zero));
    }

    [Fact]
    public void Segment_Degenerate_Throws()
    {
        var point = new Vector3d(1, 1, 1);
        Assert.Throws<ArgumentException>(() => new SnapSegment(point, point));
    }

    [Fact]
    public void Segment_MidpointAndClosestPoint()
    {
        var segment = new SnapSegment(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0));

        InferenceAssert.PointEqual(new Vector3d(5, 0, 0), segment.Midpoint);
        InferenceAssert.PointEqual(new Vector3d(7, 0, 0), segment.ClosestPoint(new Vector3d(7, 3, 0)));
        InferenceAssert.PointEqual(new Vector3d(10, 0, 0), segment.ClosestPoint(new Vector3d(12, 0, 0)));
    }

    [Fact]
    public void Face_RequiresAtLeastThreeVertices()
    {
        Assert.Throws<ArgumentException>(() => new SnapFace(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0)]));
    }

    [Fact]
    public void Face_DegenerateRing_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SnapFace(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(2, 0, 0)]));
    }

    [Fact]
    public void Face_NormalCentroidAndContainment()
    {
        var face = new SnapFace(
        [
            new Vector3d(0, 0, 0), new Vector3d(10, 0, 0), new Vector3d(10, 10, 0), new Vector3d(0, 10, 0),
        ]);

        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), face.Normal);
        InferenceAssert.PointEqual(new Vector3d(5, 5, 0), face.Centroid);
        Assert.True(face.Contains(new Vector3d(5, 5, 0), 0.01));
        Assert.False(face.Contains(new Vector3d(15, 5, 0), 0.01));
    }

    [Fact]
    public void Face_TryIntersect_ParallelRayReturnsFalse()
    {
        var face = new SnapFace(
        [
            new Vector3d(0, 0, 0), new Vector3d(10, 0, 0), new Vector3d(10, 10, 0), new Vector3d(0, 10, 0),
        ]);

        Assert.False(face.TryIntersect(new Ray3d(new Vector3d(0, 0, 5), Vector3d.UnitX), out _));

        Assert.True(face.TryIntersect(new Ray3d(new Vector3d(5, 5, 5), new Vector3d(0, 0, -1)), out Vector3d point));
        InferenceAssert.PointEqual(new Vector3d(5, 5, 0), point);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Circle_NonPositiveRadius_Throws(double radius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapCircle(Vector3d.Zero, Vector3d.UnitZ, radius));
    }

    [Fact]
    public void Circle_NormalizesNormal()
    {
        var circle = new SnapCircle(new Vector3d(1, 2, 3), new Vector3d(0, 0, 2), 5);

        InferenceAssert.DirectionEqual(new Vector3d(0, 0, 1), circle.Normal);
    }

    [Fact]
    public void Candidate_CombinedFlagRequiresDirectionAndMarker()
    {
        var point = new SnapCandidate(SnapType.AxisX, Vector3d.Zero, 1, SnapDefaults.AxisPriority, SnapMarkerKind.Line);
        Assert.False(point.IsCombined);

        var combined = point with { Direction = Vector3d.UnitX, Marker = SnapMarkerKind.LineAndPoint };
        Assert.True(combined.IsCombined);
    }

    [Fact]
    public void SnapDefaults_MappingsPerType()
    {
        Assert.Equal(SnapFilter.Endpoints, SnapDefaults.FilterOf(SnapType.Endpoint));
        Assert.Equal(SnapFilter.Axes, SnapDefaults.FilterOf(SnapType.AxisZ));
        Assert.Equal(SnapMarkerKind.Cross, SnapDefaults.MarkerOf(SnapType.Intersection));
        Assert.Equal(SnapMarkerKind.Circle, SnapDefaults.MarkerOf(SnapType.Midpoint));
    }

    [Fact]
    public void SnapFilter_AllCoversEveryType()
    {
        foreach (SnapType type in Enum.GetValues<SnapType>())
        {
            Assert.NotEqual(SnapFilter.None, SnapDefaults.FilterOf(type) & SnapFilter.All);
        }
    }

    [Fact]
    public void Request_AtCursor_BuildsRayThroughCursor()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();
        Vector2d cursor = new(425, 375);

        SnapRequest request = SnapRequest.AtCursor(viewport, InferenceScene.Empty, cursor);

        Assert.Equal(cursor, request.CursorPx);
        Assert.Equal(0, request.Ray.DistanceSquaredTo(viewport.PickRay(cursor).PointAt(50)), 12);
    }
}
