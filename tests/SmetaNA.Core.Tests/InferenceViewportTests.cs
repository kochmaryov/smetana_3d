using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class InferenceViewportTests
{
    [Fact]
    public void TryProject_CenterOfView_MapsToCenterPixels()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();

        Vector2d? projected = viewport.TryProject(new Vector3d(0, 0, 0));

        Assert.NotNull(projected);
        InferenceAssert.PointEqual(new Vector3d(400, 400, 0), new Vector3d(projected.Value.X, projected.Value.Y, 0));
    }

    [Theory]
    [InlineData(10, 0, 440, 400)]
    [InlineData(0, 10, 400, 360)]
    [InlineData(-5, -5, 380, 420)]
    public void TryProject_OnGroundPlane_UsesFourPixelsPerUnit(double x, double y, double expectedX, double expectedY)
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();

        Vector2d projected = InferenceTestSupport.Project(viewport, new Vector3d(x, y, 0));

        Assert.Equal(expectedX, projected.X, 6);
        Assert.Equal(expectedY, projected.Y, 6);
    }

    [Fact]
    public void TryProject_BehindCamera_ReturnsNull()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();

        Assert.Null(viewport.TryProject(new Vector3d(0, 0, 200)));
    }

    [Fact]
    public void DepthOf_MeasuresAlongForward()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();

        Assert.Equal(100, viewport.DepthOf(new Vector3d(0, 0, 0)), 9);
        Assert.Equal(80, viewport.DepthOf(new Vector3d(0, 0, 20)), 9);
    }

    [Fact]
    public void WorldRadiusAt_Perspective_ScalesWithDepth()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();

        double near = viewport.WorldRadiusAt(new Vector3d(0, 0, 50), 8);
        double far = viewport.WorldRadiusAt(new Vector3d(0, 0, 0), 8);

        Assert.Equal(1.0, near, 6);
        Assert.Equal(2.0, far, 6);
    }

    [Fact]
    public void WorldRadiusAt_Orthographic_IsDepthIndependent()
    {
        InferenceViewport viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(0, 0, 100), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), orthoHeight: 200, 800, 800);

        Assert.Equal(2.0, viewport.WorldRadiusAt(new Vector3d(0, 0, 50), 8), 6);
        Assert.Equal(2.0, viewport.WorldRadiusAt(new Vector3d(0, 0, 0), 8), 6);
    }

    [Fact]
    public void TryProject_Orthographic_ScalesLikePixelsPerUnit()
    {
        InferenceViewport viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(0, 0, 100), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), orthoHeight: 200, 800, 800);

        Vector2d projected = InferenceTestSupport.Project(viewport, new Vector3d(10, -5, 0));

        Assert.Equal(440, projected.X, 6);
        Assert.Equal(420, projected.Y, 6);
    }

    [Fact]
    public void PickRay_Perspective_PassesThroughPixelWorldPoint()
    {
        InferenceViewport viewport = InferenceTestSupport.TopDown();
        var worldPoint = new Vector3d(10, 10, 0);

        Ray3d ray = viewport.PickRay(InferenceTestSupport.Project(viewport, worldPoint));

        Assert.Equal(0, ray.DistanceSquaredTo(worldPoint), 9);
    }

    [Fact]
    public void PickRay_Orthographic_IsParallelToForward()
    {
        InferenceViewport viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(0, 0, 100), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), orthoHeight: 200, 800, 800);

        Ray3d ray = viewport.PickRay(InferenceTestSupport.Project(viewport, new Vector3d(10, -5, 0)));

        InferenceAssert.DirectionEqual(new Vector3d(0, 0, -1), ray.Direction);
        Assert.Equal(0, ray.DistanceSquaredTo(new Vector3d(10, -5, 0)), 9);
    }

    [Fact]
    public void CreatePerspective_ZeroForward_Throws()
    {
        Assert.Throws<ArgumentException>(() => InferenceViewport.CreatePerspective(
            Vector3d.Zero, Vector3d.Zero, Vector3d.UnitZ, Math.PI / 2, 800, 600));
    }

    [Fact]
    public void CreatePerspective_ForwardParallelToUp_Throws()
    {
        Assert.Throws<ArgumentException>(() => InferenceViewport.CreatePerspective(
            Vector3d.Zero, Vector3d.UnitZ, Vector3d.UnitZ, Math.PI / 2, 800, 600));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Math.PI)]
    public void CreatePerspective_FieldOfViewOutOfRange_Throws(double fieldOfViewY)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InferenceViewport.CreatePerspective(
            Vector3d.Zero, new Vector3d(0, 0, -1), Vector3d.UnitY, fieldOfViewY, 800, 600));
    }

    [Fact]
    public void CreateOrthographic_NonPositiveHeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InferenceViewport.CreateOrthographic(
            Vector3d.Zero, new Vector3d(0, 0, -1), Vector3d.UnitY, orthoHeight: 0, 800, 600));
    }
}
