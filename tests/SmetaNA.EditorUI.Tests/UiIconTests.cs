using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using SmetaNA.EditorUI.Icons;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки icon-pack интерфейса (<see cref="UiIconId"/>): полнота глифов по
/// контракту, точность ключей ресурсов, различимость и валидность path
/// mini-language в поле 24×24 — по образцу <see cref="ToolIconTests"/> палитры.
/// </summary>
public sealed partial class UiIconTests
{
    private const double GlyphSize = UiIcon.GlyphDesignSize;

    [Fact]
    public void GlyphResources_CoverAllUiIconIds()
    {
        UiIconId[] iconIds = ContractIconIds();
        Assert.Equal(40, iconIds.Length);

        foreach (UiIconId iconId in iconIds)
        {
            Assert.True(
                UiIconResources.TryGetPathData(iconId, out string pathData),
                $"нет глифа для {iconId}");
            Assert.False(string.IsNullOrWhiteSpace(pathData));
        }

        Assert.False(UiIconResources.TryGetPathData(UiIconId.None, out _));
    }

    [Fact]
    public void GlyphResources_HaveExactContractKeys()
    {
        string[] keys = UiIconResources.Instance.Keys
            .Select(key => key.ToString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = ContractIconIds()
            .Select(UiIconResources.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Glyphs_AreDistinct()
    {
        string[] pathData = ContractIconIds()
            .Select(iconId =>
            {
                Assert.True(UiIconResources.TryGetPathData(iconId, out string data));
                return data;
            })
            .ToArray();

        Assert.Equal(pathData.Length, pathData.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Glyphs_AreValidStrokePaths_Within24x24()
    {
        foreach (UiIconId iconId in ContractIconIds())
        {
            Assert.True(UiIconResources.TryGetPathData(iconId, out string pathData));
            Assert.Matches(PathStartRegex(), pathData);
            Assert.DoesNotContain("fill", pathData, StringComparison.OrdinalIgnoreCase);

            foreach (Match match in NumberRegex().Matches(pathData))
            {
                double value = double.Parse(match.Value, CultureInfo.InvariantCulture);

                // Абсолютные координаты — в поле; относительные дельты (l/a)
                // допускаются до ±24: главное — ловить слитные флаги дуг («100»).
                Assert.True(
                    Math.Abs(value) is >= -0.01 and <= GlyphSize + 0.01,
                    $"{iconId}: число {match.Value} вне поля {GlyphSize}×{GlyphSize}");
            }
        }
    }

    [Fact]
    public void UiIcon_RendersContractGlyph_ByDefault20px()
    {
        var icon = new UiIcon();
        Assert.Equal(UiIconId.None, icon.IconId);

        icon.IconId = UiIconId.Undo;
        Assert.Equal(UiIconId.Undo, icon.IconId);

        icon.Measure(Size.Infinity);
        Assert.Equal(new Size(UiIcon.DefaultDisplaySize, UiIcon.DefaultDisplaySize), icon.DesiredSize);
        Assert.True(UiIconResources.TryGetPathData(icon.IconId, out _));
    }

    private static UiIconId[] ContractIconIds() =>
        Enum.GetValues<UiIconId>().Where(iconId => iconId != UiIconId.None).ToArray();

    /// <summary>Путь должен начинаться с команды M/m (stroke-глиф без заливки).</summary>
    [GeneratedRegex(@"\A[Mm]")]
    private static partial Regex PathStartRegex();

    /// <summary>Все числовые токены path mini-language (координаты и радиусы дуг).</summary>
    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex NumberRegex();
}
