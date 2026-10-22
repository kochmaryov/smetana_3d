using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class LengthUnitTests
{
    [Theory]
    [InlineData(LengthUnit.Millimeter, 1.0)]
    [InlineData(LengthUnit.Centimeter, 10.0)]
    [InlineData(LengthUnit.Meter, 1000.0)]
    [InlineData(LengthUnit.Inch, 25.4)]
    public void ToMillimeters_ReturnsExpectedFactor(LengthUnit unit, double expected)
    {
        Assert.Equal(expected, unit.ToMillimeters());
    }
}
