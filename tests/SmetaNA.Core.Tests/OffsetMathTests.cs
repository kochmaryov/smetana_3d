using SmetaNA.Core.Geometry;
using SmetaNA.Core.Tools.Editing;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Математика Offset: концентрический контур в плоскости грани.</summary>
public class OffsetMathTests
{
    private static readonly IReadOnlyList<Vector3d> Square =
    [
        new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0),
    ];

    [Fact]
    public void OffsetContour_SquareInset_IsConcentricSquare()
    {
        IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(Square, Vector3d.UnitZ, 2);

        Assert.Equal(4, contour.Count);
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(2, 2, 0), contour[0]));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(8, 2, 0), contour[1]));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(8, 8, 0), contour[2]));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(2, 8, 0), contour[3]));
    }

    [Fact]
    public void OffsetContour_SquareOutset_Expands()
    {
        IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(Square, Vector3d.UnitZ, -3);

        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(-3, -3, 0), contour[0]));
        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(13, 13, 0), contour[2]));
    }

    [Fact]
    public void OffsetContour_ZeroDistance_ReturnsSameRing()
    {
        IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(Square, Vector3d.UnitZ, 0);
        Assert.Equal(Square, contour);
    }

    [Fact]
    public void OffsetContour_TriangleInset_KeepsThreeCorners()
    {
        IReadOnlyList<Vector3d> triangle = [new(0, 0, 0), new(10, 0, 0), new(0, 10, 0)];
        IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(triangle, Vector3d.UnitZ, 1);

        Assert.Equal(3, contour.Count);
        foreach (Vector3d point in contour)
        {
            // контур смещён внутрь: координаты положительны и меньше исходных
            Assert.True(point.X > 0 && point.X < 10);
            Assert.True(point.Y > 0 && point.Y < 10);
        }
    }

    [Fact]
    public void OffsetContour_DegenerateRing_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            OffsetMath.OffsetContour([new Vector3d(0, 0, 0), new Vector3d(1, 0, 0)], Vector3d.UnitZ, 1));
    }

    [Fact]
    public void OffsetContour_InvalidNormal_Throws()
    {
        Assert.Throws<ArgumentException>(() => OffsetMath.OffsetContour(Square, Vector3d.Zero, 1));
    }
}
