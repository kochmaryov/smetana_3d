using SmetaNA.Core.Geometry;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class CameraTests
{
    private static Camera FrontCamera(double distance = 10)
    {
        var camera = new Camera();
        camera.SetView(StandardView.Front);
        camera.Distance = distance;
        camera.Target = Vector3d.Zero;
        return camera;
    }

    private static void AssertVector(Vector3d expected, Vector3d actual, int precision = 9)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void SetView_Front_LooksAlongPositiveY()
    {
        Camera camera = FrontCamera();

        AssertVector(new Vector3d(0, -10, 0), camera.EyePosition);
        AssertVector(new Vector3d(0, 1, 0), camera.Forward);
        AssertVector(new Vector3d(0, 0, 1), camera.Up);
        AssertVector(new Vector3d(1, 0, 0), camera.Right);
    }

    [Fact]
    public void SetView_Back_LooksAlongNegativeY()
    {
        Camera camera = new();
        camera.SetView(StandardView.Back);
        camera.Distance = 10;

        AssertVector(new Vector3d(0, 10, 0), camera.EyePosition);
        AssertVector(new Vector3d(0, -1, 0), camera.Forward);
        AssertVector(new Vector3d(0, 0, 1), camera.Up);
        AssertVector(new Vector3d(-1, 0, 0), camera.Right);
    }

    [Fact]
    public void SetView_TopAndBottom_LookAlongZ()
    {
        var camera = new Camera { Distance = 10 };

        camera.SetView(StandardView.Top);
        AssertVector(new Vector3d(0, 0, 10), camera.EyePosition);
        AssertVector(new Vector3d(0, 0, -1), camera.Forward);
        AssertVector(new Vector3d(0, 1, 0), camera.Up);
        AssertVector(new Vector3d(1, 0, 0), camera.Right);

        camera.SetView(StandardView.Bottom);
        AssertVector(new Vector3d(0, 0, -10), camera.EyePosition);
        AssertVector(new Vector3d(0, 0, 1), camera.Forward);
        AssertVector(new Vector3d(0, -1, 0), camera.Up);
        AssertVector(new Vector3d(1, 0, 0), camera.Right);
    }

    [Fact]
    public void SetView_LeftAndRight_LookAlongX()
    {
        var camera = new Camera { Distance = 10 };

        camera.SetView(StandardView.Right);
        AssertVector(new Vector3d(10, 0, 0), camera.EyePosition);
        AssertVector(new Vector3d(-1, 0, 0), camera.Forward);
        AssertVector(new Vector3d(0, 1, 0), camera.Right);
        AssertVector(new Vector3d(0, 0, 1), camera.Up);

        camera.SetView(StandardView.Left);
        AssertVector(new Vector3d(-10, 0, 0), camera.EyePosition);
        AssertVector(new Vector3d(1, 0, 0), camera.Forward);
        AssertVector(new Vector3d(0, -1, 0), camera.Right);
        AssertVector(new Vector3d(0, 0, 1), camera.Up);
    }

    [Fact]
    public void SetView_Isometric_UsesEqualAxisAngles()
    {
        Camera camera = FrontCamera();
        camera.SetView(StandardView.Isometric);

        Vector3d direction = camera.EyeDirection;
        AssertVector(new Vector3d(1, -1, 1) * (1.0 / Math.Sqrt(3)), direction);
    }

    [Fact]
    public void ViewMatrix_MapsTargetToMinusDistanceOnViewZ()
    {
        Camera camera = FrontCamera(10);
        camera.Target = new Vector3d(3, 4, 5);

        Transform3d view = camera.ViewMatrix;

        AssertVector(new Vector3d(0, 0, -10), view.TransformPoint(camera.Target));
        AssertVector(Vector3d.Zero, view.TransformPoint(camera.EyePosition));
        AssertVector(new Vector3d(1, 0, -10), view.TransformPoint(camera.Target + Vector3d.UnitX));
        AssertVector(new Vector3d(0, 1, -10), view.TransformPoint(camera.Target + Vector3d.UnitZ));
    }

    [Fact]
    public void Orbit_MovesEyeAroundTargetKeepingDistance()
    {
        Camera camera = FrontCamera(10);

        camera.Orbit(Math.PI / 2, 0);

        AssertVector(new Vector3d(10, 0, 0), camera.EyePosition);
        Assert.Equal(10, (camera.EyePosition - camera.Target).Length(), 9);

        camera.Orbit(0, Math.PI);
        Assert.Equal(Math.PI / 2, camera.Pitch, 9);
        AssertVector(new Vector3d(0, 0, 10), camera.EyePosition);
    }

    [Fact]
    public void Pan_MovesTargetInScreenPlane()
    {
        Camera camera = FrontCamera(10);

        camera.Pan(3, 4);

        AssertVector(new Vector3d(3, 0, 4), camera.Target);
        AssertVector(new Vector3d(3, -10, 4), camera.EyePosition);
        Assert.Equal(10, (camera.EyePosition - camera.Target).Length(), 9);
    }

    [Fact]
    public void Zoom_ScalesDistanceAndOrthoHeight()
    {
        Camera camera = FrontCamera(1000);

        camera.Zoom(2);
        Assert.Equal(2000, camera.Distance, 9);
        Assert.Equal(2000, camera.OrthoHeight, 9);

        camera.Zoom(0.5);
        Assert.Equal(1000, camera.Distance, 9);
        Assert.Equal(1000, camera.OrthoHeight, 9);

        camera.Zoom(1e-12);
        Assert.Equal(Camera.MinDistance, camera.Distance, 9);
    }

    [Fact]
    public void YawWrapsAndPitchClamps()
    {
        var camera = new Camera();

        camera.Yaw = 3 * Math.PI;
        Assert.Equal(Math.PI, camera.Yaw, 9);

        camera.Yaw = 5 * Math.PI / 2;
        Assert.Equal(Math.PI / 2, camera.Yaw, 9);

        camera.Pitch = 3;
        Assert.Equal(Math.PI / 2, camera.Pitch, 9);

        camera.Pitch = -3;
        Assert.Equal(-Math.PI / 2, camera.Pitch, 9);
    }

    [Fact]
    public void Project_Perspective_MapsFieldOfViewEdges()
    {
        Camera camera = FrontCamera(10);
        camera.FieldOfViewY = Math.PI / 2;
        camera.AspectRatio = 2;
        camera.SetClipPlanes(1, 100);

        AssertVector(new Vector3d(0, 0, 0.818181818), camera.Project(Vector3d.Zero));
        AssertVector(new Vector3d(1, 0, 0.818181818), camera.Project(new Vector3d(20, 0, 0)));
        AssertVector(new Vector3d(0, 1, 0.818181818), camera.Project(new Vector3d(0, 0, 10)));
        AssertVector(new Vector3d(-1, 0, 0.818181818), camera.Project(new Vector3d(-20, 0, 0)));
    }

    [Fact]
    public void Project_Perspective_MapsClipPlanesToMinusOneAndOne()
    {
        Camera camera = FrontCamera(10);
        camera.FieldOfViewY = Math.PI / 2;
        camera.SetClipPlanes(1, 100);

        AssertVector(new Vector3d(0, 0, -1), camera.Project(new Vector3d(0, -9, 0)));
        AssertVector(new Vector3d(0, 0, 1), camera.Project(new Vector3d(0, 90, 0)));
    }

    [Fact]
    public void Project_Orthographic_MapsExtentsAndClipPlanes()
    {
        Camera camera = FrontCamera(10);
        camera.Mode = ProjectionMode.Orthographic;
        camera.OrthoHeight = 20;
        camera.AspectRatio = 2;
        camera.SetClipPlanes(1, 100);

        AssertVector(new Vector3d(1, 0, -0.818181818), camera.Project(new Vector3d(20, 0, 0)));
        AssertVector(new Vector3d(0, 1, -0.818181818), camera.Project(new Vector3d(0, 0, 10)));
        AssertVector(new Vector3d(0, 0, -1), camera.Project(new Vector3d(0, -9, 0)));
        AssertVector(new Vector3d(0, 0, 1), camera.Project(new Vector3d(0, 90, 0)));
    }

    [Theory]
    [InlineData(ProjectionMode.Perspective, 0.5)]
    [InlineData(ProjectionMode.Perspective, 1.0)]
    [InlineData(ProjectionMode.Perspective, 2.0)]
    [InlineData(ProjectionMode.Orthographic, 0.5)]
    [InlineData(ProjectionMode.Orthographic, 1.0)]
    [InlineData(ProjectionMode.Orthographic, 2.0)]
    public void ZoomExtents_FramesWholeBoundingBox(ProjectionMode mode, double aspect)
    {
        var camera = new Camera { Mode = mode, AspectRatio = aspect };
        camera.SetView(StandardView.Isometric);
        var bounds = new BoundingBox3d(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));

        camera.ZoomExtents(bounds);

        AssertVector(bounds.Center, camera.Target);
        foreach (Vector3d corner in Corners(bounds))
        {
            Vector3d ndc = camera.Project(corner);
            Assert.True(ndc.X <= 1 + 1e-9 && ndc.X >= -1 - 1e-9, $"X={ndc.X} вышел за кадр.");
            Assert.True(ndc.Y <= 1 + 1e-9 && ndc.Y >= -1 - 1e-9, $"Y={ndc.Y} вышел за кадр.");
            Assert.True(ndc.Z < 1 && ndc.Z > -1, $"Z={ndc.Z} вышел за плоскости отсечения.");
        }
    }

    [Fact]
    public void ZoomExtents_KeepsEveryViewInsideFrame()
    {
        var camera = new Camera { AspectRatio = 1.6 };
        var bounds = new BoundingBox3d(new Vector3d(-10, -5, 0), new Vector3d(10, 5, 20));

        foreach (StandardView view in Enum.GetValues<StandardView>())
        {
            camera.SetView(view);
            camera.ZoomExtents(bounds, margin: 1.1);

            foreach (Vector3d corner in Corners(bounds))
            {
                Vector3d ndc = camera.Project(corner);
                Assert.True(ndc.X <= 1 + 1e-9 && ndc.X >= -1 - 1e-9, $"Вид {view}: X={ndc.X} вышел за кадр.");
                Assert.True(ndc.Y <= 1 + 1e-9 && ndc.Y >= -1 - 1e-9, $"Вид {view}: Y={ndc.Y} вышел за кадр.");
                Assert.True(ndc.Z < 1 && ndc.Z > -1, $"Вид {view}: Z={ndc.Z} вышел за плоскости отсечения.");
            }
        }
    }

    [Fact]
    public void ZoomExtents_PointBox_IsStillUsable()
    {
        var camera = new Camera();
        var point = new BoundingBox3d(new Vector3d(5, 5, 5), new Vector3d(5, 5, 5));

        camera.ZoomExtents(point);

        AssertVector(new Vector3d(5, 5, 5), camera.Target);
        Vector3d ndc = camera.Project(new Vector3d(5, 5, 5));
        Assert.True(ndc.X >= -1 && ndc.X <= 1);
        Assert.True(ndc.Y >= -1 && ndc.Y <= 1);
    }

    [Fact]
    public void ZoomExtents_EmptyBoundsOrBadMargin_Throws()
    {
        var camera = new Camera();
        var bounds = new BoundingBox3d(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));

        Assert.Throws<ArgumentException>(() => camera.ZoomExtents(BoundingBox3d.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.ZoomExtents(bounds, margin: 0));
    }

    [Fact]
    public void InvalidSettings_Throw()
    {
        var camera = new Camera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FieldOfViewY = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.FieldOfViewY = Math.PI);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.AspectRatio = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Distance = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.OrthoHeight = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.NearPlane = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.SetClipPlanes(10, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Pitch = double.NaN);
        Assert.Throws<ArgumentException>(() => camera.Target = new Vector3d(double.NaN, 0, 0));
    }

    [Fact]
    public void SetClipPlanes_ReplacesBothAtomically()
    {
        Camera camera = FrontCamera();

        camera.SetClipPlanes(2, 200);

        Assert.Equal(2, camera.NearPlane, 9);
        Assert.Equal(200, camera.FarPlane, 9);
    }

    private static IEnumerable<Vector3d> Corners(BoundingBox3d bounds)
    {
        for (int corner = 0; corner < 8; corner++)
        {
            yield return new Vector3d(
                (corner & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (corner & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (corner & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
        }
    }
}
