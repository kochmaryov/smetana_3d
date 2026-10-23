using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Математика расстояния push/pull по лучу курсора.</summary>
public class PushPullMathTests
{
    [Fact]
    public void OffsetAlongAxis_Exact_WhenCursorRayCrossesAxis()
    {
        // ось — Z через начало; луч проходит через точку оси (0, 0, 2)
        var ray = new Ray3d(new Vector3d(0, -10, 12), new Vector3d(0, 10, -10));
        double offset = PushPullMath.OffsetAlongAxis(ray, Vector3d.Zero, Vector3d.UnitZ);

        Assert.Equal(2, offset, 9);
    }

    [Fact]
    public void OffsetAlongAxis_IsZero_AtAxisCrossing()
    {
        var ray = new Ray3d(new Vector3d(3, -10, 8), new Vector3d(-3, 10, -8));
        double offset = PushPullMath.OffsetAlongAxis(ray, Vector3d.Zero, Vector3d.UnitZ);

        Assert.Equal(0, offset, 9);
    }

    [Fact]
    public void OffsetAlongAxis_Stable_WhenRayParallelToAxis()
    {
        // луч параллелен оси (вид «в лоб»): результат конечен и слабо чувствителен
        var ray = new Ray3d(new Vector3d(1, 0, -5), Vector3d.UnitZ);
        double offset = PushPullMath.OffsetAlongAxis(ray, Vector3d.Zero, Vector3d.UnitZ);

        Assert.True(double.IsFinite(offset));
        Assert.Equal(0, offset, 9);
    }

    [Fact]
    public void OffsetAlongAxis_UsesFallback_NearParallelViews()
    {
        var ray = new Ray3d(new Vector3d(0, -10, 12), new Vector3d(0, 10, -10).Normalized());
        double expected = PushPullMath.OffsetAlongAxis(ray, Vector3d.Zero, Vector3d.UnitZ);
        Assert.Equal(2, expected, 9);

        // почти параллельный луч не даёт деления на ноль
        var nearlyParallel = new Ray3d(new Vector3d(1, 0, -5), new Vector3d(0.01, 0.01, 1).Normalized());
        Assert.True(double.IsFinite(PushPullMath.OffsetAlongAxis(nearlyParallel, Vector3d.Zero, Vector3d.UnitZ)));
    }

    [Fact]
    public void OffsetAlongAxis_Throws_OnDegenerateAxis()
    {
        var ray = new Ray3d(Vector3d.Zero, Vector3d.UnitZ);
        Assert.Throws<ArgumentException>(() => PushPullMath.OffsetAlongAxis(ray, Vector3d.Zero, Vector3d.Zero));
    }
}
