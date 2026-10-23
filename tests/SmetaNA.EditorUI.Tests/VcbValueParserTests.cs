using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Vcb;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

public sealed class VcbValueParserTests
{
    [Theory]
    [InlineData("100", 100, 100)]
    [InlineData("100.5", 100.5, 100.5)]
    [InlineData("100,5", 100.5, 100.5)]
    [InlineData("  42  ", 42, 42)]
    [InlineData("+7", 7, 7)]
    [InlineData("0", 0, 0)]
    [InlineData("10 mm", 10, 10)]
    [InlineData("5мм", 5, 5)]
    [InlineData("10 cm", 10, 100)]
    [InlineData("10см", 10, 100)]
    [InlineData("2 m", 2, 2000)]
    [InlineData("2м", 2, 2000)]
    [InlineData("1 in", 1, 25.4)]
    [InlineData("1дюйм", 1, 25.4)]
    public void ParseScalar_Length_ConvertsToMillimeters(string text, double value, double millimeters)
    {
        VcbParseResult result = VcbValueParser.ParseScalar(text, VcbInputKind.Length, LengthUnit.Millimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value, 3);
        Assert.Equal(millimeters, result.ValueInMillimeters!.Value, 3);
    }

    [Fact]
    public void ParseScalar_WithoutSuffix_UsesProjectUnit()
    {
        VcbParseResult result = VcbValueParser.ParseScalar("3", VcbInputKind.Length, LengthUnit.Centimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.ValueInMillimeters!.Value, 3);
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("90°", 90)]
    [InlineData("-45,5", -45.5)]
    [InlineData("12 deg", 12)]
    [InlineData("200 град", 200)]
    public void ParseScalar_Angle_TakesDegrees(string text, double degrees)
    {
        VcbParseResult result = VcbValueParser.ParseScalar(text, VcbInputKind.Angle, LengthUnit.Millimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(degrees, result.Value, 3);
        Assert.Null(result.ValueInMillimeters);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1 2")]
    [InlineData("мм")]
    public void ParseScalar_WrongNumber_ReturnsError(string text)
    {
        VcbParseResult result = VcbValueParser.ParseScalar(text, VcbInputKind.Length, LengthUnit.Millimeter);

        Assert.False(result.IsSuccess);
        Assert.True(result.Error is VcbError.Empty or VcbError.InvalidNumber);
    }

    [Theory]
    [InlineData("12mm2")]
    [InlineData("5 парсек")]
    [InlineData("90 mm")]
    public void ParseScalar_UnknownUnit_ReturnsUnknownUnit(string text)
    {
        VcbInputKind kind = text == "90 mm" ? VcbInputKind.Angle : VcbInputKind.Length;
        VcbParseResult result = VcbValueParser.ParseScalar(text, kind, LengthUnit.Millimeter);

        Assert.Equal(VcbError.UnknownUnit, result.Error);
    }

    [Fact]
    public void ParseScalar_InfiniteValue_ReturnsNotFinite()
    {
        VcbParseResult result = VcbValueParser.ParseScalar("1e999", VcbInputKind.Length, LengthUnit.Millimeter);

        Assert.Equal(VcbError.NotFinite, result.Error);
    }

    [Theory]
    [InlineData(VcbInputKind.Length)]
    [InlineData(VcbInputKind.Radius)]
    public void ParseScalar_Negative_ReturnsNegative(VcbInputKind kind)
    {
        VcbParseResult result = VcbValueParser.ParseScalar("-5", kind, LengthUnit.Millimeter);

        Assert.Equal(VcbError.Negative, result.Error);
    }

    [Fact]
    public void ParseScalar_Radius_Zero_ReturnsNotPositive()
    {
        VcbParseResult result = VcbValueParser.ParseScalar("0", VcbInputKind.Radius, LengthUnit.Millimeter);

        Assert.Equal(VcbError.NotPositive, result.Error);
    }

    [Fact]
    public void ParseScalar_Radius_Positive_Succeeds()
    {
        VcbParseResult result = VcbValueParser.ParseScalar("1,5", VcbInputKind.Radius, LengthUnit.Centimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(15, result.ValueInMillimeters!.Value, 3);
    }

    [Fact]
    public void ParseScalar_CoordinateKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VcbValueParser.ParseScalar("1", VcbInputKind.Coordinate, LengthUnit.Millimeter));
    }

    [Theory]
    [InlineData("1; 2; 3")]
    [InlineData("1 2 3")]
    [InlineData("1, 2, 3")]
    public void ParseCoordinate_ThreeComponents_Succeeds(string text)
    {
        VcbCoordinateResult result = VcbValueParser.ParseCoordinate(text, LengthUnit.Millimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.X, 3);
        Assert.Equal(2, result.Y, 3);
        Assert.Equal(3, result.Z, 3);
    }

    [Fact]
    public void ParseCoordinate_WithUnits_ConvertsEachComponent()
    {
        VcbCoordinateResult result = VcbValueParser.ParseCoordinate("10 см; 0,5 м; 2", LengthUnit.Millimeter);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.X, 3);
        Assert.Equal(500, result.Y, 3);
        Assert.Equal(2, result.Z, 3);
    }

    [Theory]
    [InlineData("1; 2")]
    [InlineData("1; 2; 3; 4")]
    [InlineData("1 2")]
    public void ParseCoordinate_WrongComponentCount_ReturnsComponentCount(string text)
    {
        VcbCoordinateResult result = VcbValueParser.ParseCoordinate(text, LengthUnit.Millimeter);

        Assert.Equal(VcbError.ComponentCount, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseCoordinate_Empty_ReturnsEmpty(string text)
    {
        VcbCoordinateResult result = VcbValueParser.ParseCoordinate(text, LengthUnit.Millimeter);

        Assert.Equal(VcbError.Empty, result.Error);
    }

    [Fact]
    public void ParseCoordinate_InvalidComponent_ReturnsItsError()
    {
        VcbCoordinateResult result = VcbValueParser.ParseCoordinate("1; x; 3", LengthUnit.Millimeter);

        Assert.Equal(VcbError.InvalidNumber, result.Error);
    }
}
