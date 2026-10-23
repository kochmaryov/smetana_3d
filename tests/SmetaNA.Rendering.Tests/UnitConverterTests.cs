using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class UnitConverterTests
{
    [Theory]
    [InlineData(LengthUnit.Millimeter, 1.0)]
    [InlineData(LengthUnit.Centimeter, 0.1)]
    [InlineData(LengthUnit.Meter, 0.001)]
    [InlineData(LengthUnit.Inch, 1.0 / 25.4)]
    public void ToUnits_ConvertsFromMillimeters(LengthUnit unit, double expected)
    {
        Assert.Equal(expected, UnitConverter.ToUnits(1.0, unit), 9);
    }

    [Theory]
    [InlineData(LengthUnit.Millimeter)]
    [InlineData(LengthUnit.Centimeter)]
    [InlineData(LengthUnit.Meter)]
    [InlineData(LengthUnit.Inch)]
    public void RoundTrip_PreservesLength(LengthUnit unit)
    {
        double millimeters = 123.456;

        double roundTrip = UnitConverter.ToMillimeters(UnitConverter.ToUnits(millimeters, unit), unit);

        Assert.Equal(millimeters, roundTrip, 6);
    }
}
