using System.Globalization;
using System.Reflection;
using Avalonia;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки набора иконок палитры (трек A волны 1 tool-palette, T1/T6, DoD
/// §5.1/§5.5): полнота глифов по контракту <see cref="ToolIconId"/>, валидность
/// path-данных поля 24×24, различимость глифов и покрытие
/// <see cref="ToolIconMap"/> всех команд реестра (включая под-инструменты вылетов).
/// </summary>
public sealed class ToolIconTests
{
    private const double GlyphSize = ToolIcon.GlyphDesignSize;

    [Fact]
    public void GlyphResources_CoverAllToolIconIds()
    {
        ToolIconId[] iconIds = ContractIconIds();
        Assert.Equal(24, iconIds.Length);

        foreach (ToolIconId iconId in iconIds)
        {
            Assert.True(
                ToolIconResources.TryGetPathData(iconId, out string pathData),
                $"нет глифа для {iconId}");
            Assert.False(string.IsNullOrWhiteSpace(pathData));
        }

        Assert.False(ToolIconResources.TryGetPathData(ToolIconId.None, out _));
    }

    [Fact]
    public void GlyphResources_HaveExactContractKeys()
    {
        string[] keys = ToolIconResources.Instance.Keys
            .Select(key => key.ToString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = ContractIconIds()
            .Select(ToolIconResources.Key)
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
                Assert.True(ToolIconResources.TryGetPathData(iconId, out string data));
                return data;
            })
            .ToArray();

        Assert.Equal(pathData.Length, pathData.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Glyphs_AreValidStrokePaths_Within24x24()
    {
        foreach (ToolIconId iconId in ContractIconIds())
        {
            Assert.True(ToolIconResources.TryGetPathData(iconId, out string pathData));
            List<(double X, double Y)> points = ParseAnchorPoints(pathData, iconId);

            Assert.True(points.Count >= 2, $"{iconId}: в глифе меньше двух точек");
            foreach ((double x, double y) in points)
            {
                Assert.True(
                    x is >= -0.01 and <= GlyphSize + 0.01 && y is >= -0.01 and <= GlyphSize + 0.01,
                    $"{iconId}: точка ({x.ToString(CultureInfo.InvariantCulture)}, {y.ToString(CultureInfo.InvariantCulture)}) вне поля {GlyphSize}×{GlyphSize}");
            }
        }
    }

    [Fact]
    public void ToolIconMap_MapsEveryToolCommandToAGlyph()
    {
        string[] toolCommands = typeof(ShortcutCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.Name.StartsWith("Tool", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(25, toolCommands.Length); // 24 инструмента + «Дуга (3 точки)»; Зеркало ×3 входит
        foreach (string commandId in toolCommands)
        {
            Assert.NotEqual(ToolIconId.None, ToolIconMap.ForCommand(commandId));
        }

        Assert.NotEqual(ToolIconId.None, ToolIconMap.ForCommand(ToolPaletteCommandIds.PrintVolume));
        Assert.NotEqual(ToolIconId.None, ToolIconMap.ForCommand(ToolPaletteCommandIds.Tutorial));
    }

    [Fact]
    public void RegistryButtons_HaveGlyphs_IncludingFlyoutItems()
    {
        foreach (PaletteButton button in ToolGroupRegistry.DefaultGroups.SelectMany(group => group.Buttons))
        {
            AssertButtonHasGlyph(button);
            foreach (PaletteButton flyoutItem in button.FlyoutItems)
            {
                AssertButtonHasGlyph(flyoutItem);
            }
        }
    }

    [Fact]
    public void ToolIcon_RendersContractGlyph_ByDefault20px()
    {
        var icon = new ToolIcon();
        Assert.Equal(ToolIconId.None, icon.IconId);

        icon.IconId = ToolIconId.Line;
        Assert.Equal(ToolIconId.Line, icon.IconId);

        icon.Measure(Size.Infinity);
        Assert.Equal(new Size(ToolIcon.DefaultDisplaySize, ToolIcon.DefaultDisplaySize), icon.DesiredSize);
        Assert.True(ToolIconResources.TryGetPathData(icon.IconId, out _));
    }

    private static ToolIconId[] ContractIconIds() =>
        Enum.GetValues<ToolIconId>().Where(iconId => iconId != ToolIconId.None).ToArray();

    private static void AssertButtonHasGlyph(PaletteButton button)
    {
        Assert.NotEqual(ToolIconId.None, button.IconId);
        Assert.True(
            ToolIconResources.TryGetPathData(button.IconId, out _),
            $"нет глифа для {button.Definition.CommandId}");
        Assert.Equal(ToolIconMap.ForCommand(button.Definition.CommandId), button.IconId);
    }

    /// <summary>
    /// Разбор path mini-language (глифы — stroke, без заливки): грамматика команд
    /// M/L/H/V/C/S/Q/T/A/Z, якорные и контрольные точки в поле 24×24. Замена
    /// парсинга <c>StreamGeometry</c> — он требует платформы Avalonia, а тесты
    /// юнитовые.
    /// </summary>
    private static List<(double X, double Y)> ParseAnchorPoints(string pathData, ToolIconId iconId)
    {
        var points = new List<(double X, double Y)>();
        int i = 0;
        char command = '\0';
        bool firstGroupOfCommand = true;
        double x = 0, y = 0;
        double subpathX = 0, subpathY = 0;
        double ctrlX = 0, ctrlY = 0;
        char previous = '\0';

        SkipSeparators(pathData, ref i);
        if (i >= pathData.Length || pathData[i] is not ('M' or 'm'))
        {
            throw new FormatException($"{iconId}: путь должен начинаться с M/m");
        }

        while (i < pathData.Length)
        {
            SkipSeparators(pathData, ref i);
            if (i >= pathData.Length)
            {
                break;
            }

            if (IsCommand(pathData[i]))
            {
                command = pathData[i];
                firstGroupOfCommand = true;
                i++;
            }
            else if (command is '\0' or 'Z' or 'z')
            {
                throw new FormatException($"{iconId}: ожидалась команда пути, найдено '{pathData[i]}'");
            }

            if (command is 'Z' or 'z')
            {
                x = subpathX;
                y = subpathY;
                points.Add((x, y));
                command = '\0';
                previous = 'z';
                continue;
            }

            char effective = command == 'M' || command == 'm'
                ? (firstGroupOfCommand ? command : command == 'M' ? 'L' : 'l')
                : command;

            switch (effective)
            {
                case 'M':
                    (x, y) = ReadPair(pathData, ref i);
                    subpathX = x;
                    subpathY = y;
                    points.Add((x, y));
                    break;
                case 'm':
                    (x, y) = Offset(x, y, ReadPair(pathData, ref i));
                    subpathX = x;
                    subpathY = y;
                    points.Add((x, y));
                    break;
                case 'L':
                    (x, y) = ReadPair(pathData, ref i);
                    points.Add((x, y));
                    break;
                case 'l':
                    (x, y) = Offset(x, y, ReadPair(pathData, ref i));
                    points.Add((x, y));
                    break;
                case 'H':
                    x = ReadNumber(pathData, ref i);
                    points.Add((x, y));
                    break;
                case 'h':
                    x += ReadNumber(pathData, ref i);
                    points.Add((x, y));
                    break;
                case 'V':
                    y = ReadNumber(pathData, ref i);
                    points.Add((x, y));
                    break;
                case 'v':
                    y += ReadNumber(pathData, ref i);
                    points.Add((x, y));
                    break;
                case 'C':
                    (x, y, ctrlX, ctrlY) = ReadCurve(pathData, ref i, x, y, relative: false, points);
                    break;
                case 'c':
                    (x, y, ctrlX, ctrlY) = ReadCurve(pathData, ref i, x, y, relative: true, points);
                    break;
                case 'S':
                    (x, y, ctrlX, ctrlY) = ReadSmoothCurve(pathData, ref i, x, y, previous, ctrlX, ctrlY, relative: false, points);
                    break;
                case 's':
                    (x, y, ctrlX, ctrlY) = ReadSmoothCurve(pathData, ref i, x, y, previous, ctrlX, ctrlY, relative: true, points);
                    break;
                case 'Q':
                    (x, y, ctrlX, ctrlY) = ReadQuad(pathData, ref i, x, y, relative: false, points);
                    break;
                case 'q':
                    (x, y, ctrlX, ctrlY) = ReadQuad(pathData, ref i, x, y, relative: true, points);
                    break;
                case 'T':
                    (x, y, ctrlX, ctrlY) = ReadSmoothQuad(pathData, ref i, x, y, previous, ctrlX, ctrlY, relative: false, points);
                    break;
                case 't':
                    (x, y, ctrlX, ctrlY) = ReadSmoothQuad(pathData, ref i, x, y, previous, ctrlX, ctrlY, relative: true, points);
                    break;
                case 'A':
                    (x, y) = ReadArc(pathData, ref i, x, y, relative: false, points);
                    break;
                case 'a':
                    (x, y) = ReadArc(pathData, ref i, x, y, relative: true, points);
                    break;
                default:
                    throw new FormatException($"{iconId}: неизвестная команда '{command}'");
            }

            firstGroupOfCommand = false;
            previous = effective;
        }

        return points;
    }

    private static (double EndX, double EndY, double CtrlX, double CtrlY) ReadCurve(
        string data, ref int i, double x, double y, bool relative, List<(double X, double Y)> points)
    {
        (double ctrl1X, double ctrl1Y) = ReadPair(data, ref i);
        (double ctrl2X, double ctrl2Y) = ReadPair(data, ref i);
        (double endX, double endY) = ReadPair(data, ref i);
        if (relative)
        {
            (ctrl1X, ctrl1Y) = Offset(x, y, (ctrl1X, ctrl1Y));
            (ctrl2X, ctrl2Y) = Offset(x, y, (ctrl2X, ctrl2Y));
            (endX, endY) = Offset(x, y, (endX, endY));
        }

        points.Add((ctrl1X, ctrl1Y));
        points.Add((ctrl2X, ctrl2Y));
        points.Add((endX, endY));
        return (endX, endY, ctrl2X, ctrl2Y);
    }

    private static (double EndX, double EndY, double CtrlX, double CtrlY) ReadSmoothCurve(
        string data, ref int i, double x, double y, char previous, double ctrlX, double ctrlY, bool relative,
        List<(double X, double Y)> points)
    {
        (double ctrl1X, double ctrl1Y) = previous is 'C' or 'c' or 'S' or 's'
            ? (2 * x - ctrlX, 2 * y - ctrlY)
            : (x, y);
        (double ctrl2X, double ctrl2Y) = ReadPair(data, ref i);
        (double endX, double endY) = ReadPair(data, ref i);
        if (relative)
        {
            (ctrl2X, ctrl2Y) = Offset(x, y, (ctrl2X, ctrl2Y));
            (endX, endY) = Offset(x, y, (endX, endY));
        }

        points.Add((ctrl1X, ctrl1Y));
        points.Add((ctrl2X, ctrl2Y));
        points.Add((endX, endY));
        return (endX, endY, ctrl2X, ctrl2Y);
    }

    private static (double EndX, double EndY, double CtrlX, double CtrlY) ReadQuad(
        string data, ref int i, double x, double y, bool relative, List<(double X, double Y)> points)
    {
        (double quadX, double quadY) = ReadPair(data, ref i);
        (double endX, double endY) = ReadPair(data, ref i);
        if (relative)
        {
            (quadX, quadY) = Offset(x, y, (quadX, quadY));
            (endX, endY) = Offset(x, y, (endX, endY));
        }

        points.Add((quadX, quadY));
        points.Add((endX, endY));
        return (endX, endY, quadX, quadY);
    }

    private static (double EndX, double EndY, double CtrlX, double CtrlY) ReadSmoothQuad(
        string data, ref int i, double x, double y, char previous, double ctrlX, double ctrlY, bool relative,
        List<(double X, double Y)> points)
    {
        (double quadX, double quadY) = previous is 'Q' or 'q' or 'T' or 't'
            ? (2 * x - ctrlX, 2 * y - ctrlY)
            : (x, y);
        (double endX, double endY) = ReadPair(data, ref i);
        if (relative)
        {
            (endX, endY) = Offset(x, y, (endX, endY));
        }

        points.Add((quadX, quadY));
        points.Add((endX, endY));
        return (endX, endY, quadX, quadY);
    }

    private static (double X, double Y) ReadArc(
        string data, ref int i, double x, double y, bool relative, List<(double X, double Y)> points)
    {
        ReadNumber(data, ref i); // радиус X
        ReadNumber(data, ref i); // радиус Y
        ReadNumber(data, ref i); // поворот
        ReadFlag(data, ref i);   // large-arc
        ReadFlag(data, ref i);   // sweep
        (double endX, double endY) = ReadPair(data, ref i);
        if (relative)
        {
            (endX, endY) = Offset(x, y, (endX, endY));
        }

        points.Add((endX, endY));
        return (endX, endY);
    }

    private static (double X, double Y) ReadPair(string data, ref int i) =>
        (ReadNumber(data, ref i), ReadNumber(data, ref i));

    private static (double X, double Y) Offset(double x, double y, (double X, double Y) delta) =>
        (x + delta.X, y + delta.Y);

    private static double ReadNumber(string data, ref int i)
    {
        SkipSeparators(data, ref i);
        int start = i;
        if (i < data.Length && data[i] is '+' or '-')
        {
            i++;
        }

        bool hasDigits = SkipDigits(data, ref i);
        if (i < data.Length && data[i] == '.')
        {
            i++;
            hasDigits |= SkipDigits(data, ref i);
        }

        if (!hasDigits)
        {
            throw new FormatException($"ожидалось число в позиции {start}");
        }

        if (i < data.Length && data[i] is 'e' or 'E')
        {
            int exponent = i;
            i++;
            if (i < data.Length && data[i] is '+' or '-')
            {
                i++;
            }

            if (SkipDigits(data, ref i))
            {
                return double.Parse(data[start..i], NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            i = exponent;
        }

        return double.Parse(data[start..i], NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int ReadFlag(string data, ref int i)
    {
        SkipSeparators(data, ref i);
        if (i >= data.Length || data[i] is not ('0' or '1'))
        {
            throw new FormatException($"ожидался флаг 0/1 в позиции {i}");
        }

        return data[i++] - '0';
    }

    private static bool SkipDigits(string data, ref int i)
    {
        int start = i;
        while (i < data.Length && char.IsDigit(data[i]))
        {
            i++;
        }

        return i > start;
    }

    private static void SkipSeparators(string data, ref int i)
    {
        while (i < data.Length && (char.IsWhiteSpace(data[i]) || data[i] == ','))
        {
            i++;
        }
    }

    private static bool IsCommand(char c) => c is 'M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v'
        or 'C' or 'c' or 'S' or 's' or 'Q' or 'q' or 'T' or 't' or 'A' or 'a' or 'Z' or 'z';
}
