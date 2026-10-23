using SmetaNA.Core.Geometry;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class CameraRayTests
{
    private static Camera TestCamera(ProjectionMode mode)
    {
        var camera = new Camera
        {
            Mode = mode,
            AspectRatio = 2.0,
            Distance = 100,
            Target = Vector3d.Zero,
        };
        camera.SetView(StandardView.Isometric);
        return camera;
    }

    [Fact]
    public void CenterRay_Perspective_PointsAlongForward()
    {
        Camera camera = TestCamera(ProjectionMode.Perspective);

        Ray3d ray = CameraRay.FromViewportPoint(camera, 400, 300, 800, 600);

        Assert.Equal(camera.Forward.X, ray.Direction.X, 9);
        Assert.Equal(camera.Forward.Y, ray.Direction.Y, 9);
        Assert.Equal(camera.Forward.Z, ray.Direction.Z, 9);
        Assert.Equal(camera.EyePosition.X, ray.Origin.X, 9);
        Assert.Equal(camera.EyePosition.Y, ray.Origin.Y, 9);
        Assert.Equal(camera.EyePosition.Z, ray.Origin.Z, 9);
    }

    [Fact]
    public void CenterRay_Orthographic_StartsAtEyeCenter()
    {
        Camera camera = TestCamera(ProjectionMode.Orthographic);

        Ray3d ray = CameraRay.FromViewportPoint(camera, 400, 300, 800, 600);

        // Центр кадра — параллельный луч через точку обзора камеры.
        Assert.Equal(camera.EyePosition.X, ray.Origin.X, 6);
        Assert.Equal(camera.EyePosition.Y, ray.Origin.Y, 6);
        Assert.Equal(camera.EyePosition.Z, ray.Origin.Z, 6);
        Assert.Equal(camera.Forward.X, ray.Direction.X, 9);
    }

    [Theory]
    [InlineData(ProjectionMode.Perspective)]
    [InlineData(ProjectionMode.Orthographic)]
    public void Ray_ThroughProjectedPoint_PassesNearThePoint(ProjectionMode mode)
    {
        Camera camera = TestCamera(mode);
        var point = new Vector3d(20, -12, 5);

        Vector3d ndc = camera.Project(point);
        double pixelX = ((ndc.X + 1.0) * 0.5) * 800;
        double pixelY = ((1.0 - ndc.Y) * 0.5) * 600;

        Ray3d ray = CameraRay.FromViewportPoint(camera, pixelX, pixelY, 800, 600);
        bool hit = RayIntersector.TryIntersectTriangle(
            ray,
            point + new Vector3d(-5, -5, 0),
            point + new Vector3d(5, -5, 0),
            point + new Vector3d(0, 5, 0),
            out double t,
            out _,
            out _);

        Assert.True(hit);
        Vector3d onRay = ray.GetPoint(t);
        Assert.Equal(point.X, onRay.X, 3);
        Assert.Equal(point.Y, onRay.Y, 3);
    }
}
