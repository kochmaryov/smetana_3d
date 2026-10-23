using SmetaNA.Core.Inference;
using SmetaNA.Rendering.Annotations;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>Штриховой шрифт подписей размеров.</summary>
public sealed class StrokeFontTests
{
    [Fact]
    public void MeasureWidth_PositiveForDigitsAndUnits()
    {
        Assert.True(StrokeFont.MeasureWidth("100,00 mm") > 0);
        Assert.Equal(0, StrokeFont.MeasureWidth(string.Empty));
    }

    [Fact]
    public void BuildStrokes_ScalesWithHeight()
    {
        IReadOnlyList<(Vector2d From, Vector2d To)> strokes = StrokeFont.BuildStrokes("0", 10);

        Assert.NotEmpty(strokes);
        foreach ((Vector2d from, Vector2d to) in strokes)
        {
            Assert.InRange(from.Y, -0.01, 10.01);
            Assert.InRange(to.Y, -0.01, 10.01);
            Assert.InRange(from.X, -0.01, 10.01);
        }
    }

    [Fact]
    public void BuildStrokes_UnsupportedGlyph_IsSkipped()
    {
        Assert.Empty(StrokeFont.BuildStrokes("я", 10));
        Assert.False(StrokeFont.IsSupported('я'));
        Assert.True(StrokeFont.IsSupported('5'));
        Assert.True(StrokeFont.IsSupported('M'));
    }

    [Fact]
    public void BuildStrokes_MeasureWidthMatchesAdvances()
    {
        double width = StrokeFont.MeasureWidth("12 mm");
        IReadOnlyList<(Vector2d From, Vector2d To)> strokes = StrokeFont.BuildStrokes("12 mm", 1);

        Assert.NotEmpty(strokes);
        Assert.True(width > 0);
        Assert.True(strokes[^1].To.X <= width + 1e-9);
    }

    [Fact]
    public void BuildStrokes_InvalidHeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StrokeFont.BuildStrokes("1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StrokeFont.BuildStrokes("1", -2));
    }
}
