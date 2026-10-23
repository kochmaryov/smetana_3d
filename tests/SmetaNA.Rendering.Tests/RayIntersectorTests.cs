using SmetaNA.Core.Geometry;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class RayIntersectorTests
{
    private static readonly Vector3d A = new(0, 0, 0);
    private static readonly Vector3d B = new(10, 0, 0);
    private static readonly Vector3d C = new(0, 10, 0);

    [Fact]
    public void Triangle_Hit_ReturnsBarycentrics()
    {
        var ray = new Ray3d(new Vector3d(2, 2, 5), new Vector3d(0, 0, -1));

        bool hit = RayIntersector.TryIntersectTriangle(ray, A, B, C, out double t, out double u, out double v);

        Assert.True(hit);
        Assert.Equal(5.0, t, 9);
        Assert.Equal(0.2, u, 9);
        Assert.Equal(0.2, v, 9);
    }

    [Fact]
    public void Triangle_MissOutside_ReturnsFalse()
    {
        var ray = new Ray3d(new Vector3d(9, 9, 5), new Vector3d(0, 0, -1));

        Assert.False(RayIntersector.TryIntersectTriangle(ray, A, B, C, out _, out _, out _));
    }

    [Fact]
    public void Triangle_ParallelRay_ReturnsFalse()
    {
        var ray = new Ray3d(new Vector3d(1, 1, 5), new Vector3d(1, 0, 0));

        Assert.False(RayIntersector.TryIntersectTriangle(ray, A, B, C, out _, out _, out _));
    }

    [Fact]
    public void Triangle_BehindRayOrigin_ReturnsFalse()
    {
        var ray = new Ray3d(new Vector3d(1, 1, -5), new Vector3d(0, 0, -1));

        Assert.False(RayIntersector.TryIntersectTriangle(ray, A, B, C, out _, out _, out _));
    }

    [Fact]
    public void Box_Hit_ReturnsEntryDistance()
    {
        var box = new BoundingBox3d(new Vector3d(-1, -1, -1), new Vector3d(1, 1, 1));
        var ray = new Ray3d(new Vector3d(0, 0, -5), new Vector3d(0, 0, 1));

        bool hit = RayIntersector.TryIntersectBox(ray, box, out double tEnter);

        Assert.True(hit);
        Assert.Equal(4.0, tEnter, 9);
    }

    [Fact]
    public void Box_RayInside_ReturnsZeroEntry()
    {
        var box = new BoundingBox3d(new Vector3d(-1, -1, -1), new Vector3d(1, 1, 1));
        var ray = new Ray3d(Vector3d.Zero, new Vector3d(1, 0, 0));

        bool hit = RayIntersector.TryIntersectBox(ray, box, out double tEnter);

        Assert.True(hit);
        Assert.Equal(0.0, tEnter, 9);
    }

    [Fact]
    public void Box_Miss_ReturnsFalse()
    {
        var box = new BoundingBox3d(new Vector3d(-1, -1, -1), new Vector3d(1, 1, 1));
        var ray = new Ray3d(new Vector3d(5, 5, -5), new Vector3d(0, 0, 1));

        Assert.False(RayIntersector.TryIntersectBox(ray, box, out _));
    }
}
