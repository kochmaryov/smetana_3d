using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Размеры: вычисление длин и подписей в единицах проекта.
/// Точность — ≤ 0,01 мм по DoD §5.4.
/// </summary>
public class DimensionMathTests
{
    [Fact]
    public void AlignedLength_PythagoreanSegment_IsExact()
    {
        double length = DimensionMath.AlignedLength(new Vector3d(0, 0, 0), new Vector3d(30, 40, 0));

        Assert.Equal(50, length, 9);
    }

    [Fact]
    public void AlignedLength_RotatedSegment_WithinDodTolerance()
    {
        Vector3d direction = new Vector3d(1, 2, 3).Normalized();
        Vector3d start = new(-13.7, 42.1, 8.35);
        Vector3d end = start + direction * 123.45;

        double length = DimensionMath.AlignedLength(start, end);

        Assert.True(Math.Abs(length - 123.45) <= DimensionMath.ToleranceMm, $"погрешность {Math.Abs(length - 123.45)} мм");
    }

    [Fact]
    public void LinearLength_ProjectsOntoDirection()
    {
        double length = DimensionMath.LinearLength(
            new Vector3d(0, 0, 0),
            new Vector3d(30, 40, 5),
            Vector3d.UnitX);

        Assert.Equal(30, length, 9);
    }

    [Fact]
    public void LinearLength_DiagonalDirection_UsesDotProduct()
    {
        Vector3d direction = new(1, 1, 0);
        double length = DimensionMath.LinearLength(new Vector3d(0, 0, 0), new Vector3d(10, 10, 0), direction);

        Assert.Equal(Math.Sqrt(200), length, 9);
    }

    [Fact]
    public void LinearLength_WithoutDirection_UsesPrincipalAxis()
    {
        double length = DimensionMath.LinearLength(new Vector3d(0, 0, 0), new Vector3d(3, 40, 5), null);

        Assert.Equal(40, length, 9);
    }

    [Fact]
    public void MeasuredEnd_Linear_ProjectsEndOntoDirection()
    {
        Vector3d measuredEnd = DimensionMath.MeasuredEnd(
            DimensionKind.Linear,
            new Vector3d(0, 0, 0),
            new Vector3d(30, 40, 5),
            Vector3d.UnitX);

        Assert.True(ToolsTestSupport.NearlyEqual(new Vector3d(30, 0, 0), measuredEnd));
    }

    [Fact]
    public void MeasuredEnd_Aligned_KeepsEnd()
    {
        Vector3d end = new(3, 4, 5);
        Vector3d measuredEnd = DimensionMath.MeasuredEnd(DimensionKind.Aligned, Vector3d.Zero, end, null);

        Assert.Equal(end, measuredEnd);
    }

    [Fact]
    public void MeasuredLength_KindSelectsAlignedOrLinear()
    {
        Vector3d start = new(1, 1, 1);
        Vector3d end = new(31, 41, 6);

        Assert.Equal(Math.Sqrt(2525), DimensionMath.MeasuredLength(DimensionKind.Aligned, start, end, null), 9);
        Assert.Equal(30, DimensionMath.MeasuredLength(DimensionKind.Linear, start, end, Vector3d.UnitX), 9);
    }

    [Fact]
    public void PrincipalAxis_PicksDominantComponent()
    {
        Assert.Equal(Vector3d.UnitX, DimensionMath.PrincipalAxis(new Vector3d(-12, 3, 4)));
        Assert.Equal(Vector3d.UnitY, DimensionMath.PrincipalAxis(new Vector3d(1, 30, 4)));
        Assert.Equal(Vector3d.UnitZ, DimensionMath.PrincipalAxis(new Vector3d(1, 3, -40)));
        Assert.Equal(Vector3d.Zero, DimensionMath.PrincipalAxis(Vector3d.Zero));
    }

    [Fact]
    public void DirectionOf_ExplicitDirection_IsNormalized()
    {
        Vector3d direction = DimensionMath.DirectionOf(Vector3d.Zero, new Vector3d(10, 0, 0), new Vector3d(0, 5, 0));

        Assert.True(ToolsTestSupport.NearlyEqual(Vector3d.UnitY, direction));
    }

    [Theory]
    [InlineData(100.0, LengthUnit.Millimeter, "100,00 mm")]
    [InlineData(123.456, LengthUnit.Millimeter, "123,46 mm")]
    [InlineData(123.456, LengthUnit.Centimeter, "12,346 cm")]
    [InlineData(1234.56, LengthUnit.Meter, "1,23456 m")]
    [InlineData(25.4, LengthUnit.Inch, "1,0000 in")]
    public void Format_UsesProjectUnitAndDodPrecision(double lengthMm, LengthUnit unit, string expected)
    {
        Assert.Equal(expected, DimensionText.Format(lengthMm, unit));
    }

    [Fact]
    public void Format_TextOverride_WinsOverValue()
    {
        Assert.Equal("≈ 100 mm", DimensionText.Format(100, LengthUnit.Millimeter, "≈ 100 mm"));
    }

    [Fact]
    public void Format_NonFiniteValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DimensionText.Format(double.NaN, LengthUnit.Millimeter));
    }

    [Fact]
    public void Decimals_ResolveDodResolution()
    {
        // 0,01 мм в единицах проекта помещается в разрядность подписи.
        Assert.Equal(2, DimensionText.Decimals(LengthUnit.Millimeter));
        Assert.Equal(3, DimensionText.Decimals(LengthUnit.Centimeter));
        Assert.Equal(5, DimensionText.Decimals(LengthUnit.Meter));
        Assert.Equal(4, DimensionText.Decimals(LengthUnit.Inch));
    }
}
