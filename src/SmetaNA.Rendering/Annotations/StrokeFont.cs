using SmetaNA.Core.Inference;

namespace SmetaNA.Rendering.Annotations;

/// <summary>
/// Простой штриховой (векторный) шрифт для подписей размеров: глифы — ломаные
/// в нормализованной ячейке (базовая линия y = 0, высота прописных y = 1).
/// Шрифт не зависит от шрифтовых движков ОС, одинаков во всех платформенных
/// бэкендах и батчится с линиями размеров в один буфер (GL_LINES).
/// <para>Набор: цифры, запятая/точка, минус/плюс, пробел и латинские
/// обозначения единиц mm/cm/m/in (как у разбора VCB).</para>
/// </summary>
public static class StrokeFont
{
    /// <summary>Высота глифа в нормализованных координатах.</summary>
    public const double GlyphHeight = 1.0;

    /// <summary>Поддерживается ли символ.</summary>
    public static bool IsSupported(char c) => TryGetGlyph(c, out _, out _);

    /// <summary>Ширина текста в высотах глифа (с учётом межсимвольных отступов).</summary>
    public static double MeasureWidth(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        double width = 0;
        foreach (char c in text)
        {
            TryGetGlyph(c, out double glyphWidth, out _);
            width += Advance(c, glyphWidth);
        }

        return width > 0 ? width - LetterSpacing : 0;
    }

    /// <summary>
    /// Штрихи текста: начало — левый нижний угол (базовая линия), y вверх,
    /// масштаб — высота глифа <paramref name="height"/>.
    /// </summary>
    public static IReadOnlyList<(Vector2d From, Vector2d To)> BuildStrokes(string text, double height)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Высота текста должна быть положительной.");
        }

        var strokes = new List<(Vector2d From, Vector2d To)>();
        double x = 0;
        foreach (char c in text)
        {
            if (TryGetGlyph(c, out double width, out Vector2d[][] polylines))
            {
                foreach (Vector2d[] polyline in polylines)
                {
                    for (int i = 1; i < polyline.Length; i++)
                    {
                        Vector2d from = polyline[i - 1];
                        Vector2d to = polyline[i];
                        strokes.Add((
                            new Vector2d((x + from.X) * height, from.Y * height),
                            new Vector2d((x + to.X) * height, to.Y * height)));
                    }
                }
            }

            x += Advance(c, width);
        }

        return strokes;
    }

    private const double LetterSpacing = 0.12;

    private static double Advance(char c, double width = 0)
    {
        if (c == ' ')
        {
            return 0.35;
        }

        return (width > 0 ? width : 0.5) + LetterSpacing;
    }

    private static Vector2d P(double x, double y) => new(x, y);

    private static bool TryGetGlyph(char c, out double width, out Vector2d[][] polylines)
    {
        switch (char.ToLowerInvariant(c))
        {
            case '0':
                width = 0.5;
                polylines = [[
                    P(0.25, 1), P(0.5, 0.75), P(0.5, 0.25), P(0.25, 0),
                    P(0, 0.25), P(0, 0.75), P(0.25, 1)]];
                return true;

            case '1':
                width = 0.5;
                polylines = [[P(0.05, 0.8), P(0.25, 1), P(0.25, 0)], [P(0.1, 0), P(0.4, 0)]];
                return true;

            case '2':
                width = 0.5;
                polylines = [[P(0, 0.75), P(0.15, 1), P(0.35, 1), P(0.5, 0.8), P(0.5, 0.6), P(0, 0), P(0.55, 0)]];
                return true;

            case '3':
                width = 0.5;
                polylines = [
                    [P(0, 1), P(0.5, 1), P(0.25, 0.55)],
                    [P(0.25, 0.55), P(0.45, 0.55), P(0.5, 0.4), P(0.5, 0.2), P(0.4, 0), P(0.15, 0), P(0, 0.15)]];
                return true;

            case '4':
                width = 0.5;
                polylines = [[P(0.4, 0), P(0.4, 1), P(0, 0.3), P(0.55, 0.3)]];
                return true;

            case '5':
                width = 0.5;
                polylines = [[
                    P(0.5, 1), P(0.05, 1), P(0, 0.55), P(0.35, 0.55), P(0.5, 0.4),
                    P(0.5, 0.2), P(0.35, 0), P(0.1, 0), P(0, 0.15)]];
                return true;

            case '6':
                width = 0.5;
                polylines = [[
                    P(0.45, 0.9), P(0.25, 1), P(0.05, 0.8), P(0, 0.45), P(0.05, 0.15),
                    P(0.25, 0), P(0.45, 0.1), P(0.5, 0.3), P(0.45, 0.5), P(0.2, 0.55),
                    P(0.02, 0.45), P(0, 0.3)]];
                return true;

            case '7':
                width = 0.5;
                polylines = [[P(0, 1), P(0.55, 1), P(0.2, 0)]];
                return true;

            case '8':
                width = 0.5;
                polylines = [[
                    P(0.25, 0.55), P(0.05, 0.68), P(0, 0.85), P(0.15, 1), P(0.35, 1),
                    P(0.5, 0.85), P(0.45, 0.68), P(0.25, 0.55), P(0.05, 0.42), P(0, 0.2),
                    P(0.15, 0), P(0.35, 0), P(0.5, 0.2), P(0.45, 0.42), P(0.25, 0.55)]];
                return true;

            case '9':
                width = 0.5;
                polylines = [[
                    P(0.1, 0.1), P(0.3, 0), P(0.5, 0.2), P(0.5, 0.55), P(0.45, 0.85),
                    P(0.25, 1), P(0.05, 0.9), P(0, 0.7), P(0.05, 0.5), P(0.3, 0.45),
                    P(0.48, 0.55)]];
                return true;

            case ',':
                width = 0.3;
                polylines = [[P(0.15, 0.12), P(0.02, -0.18)]];
                return true;

            case '.':
                width = 0.3;
                polylines = [[P(0.06, 0.02), P(0.14, 0.02)]];
                return true;

            case '-':
                width = 0.5;
                polylines = [[P(0.05, 0.5), P(0.45, 0.5)]];
                return true;

            case '+':
                width = 0.5;
                polylines = [[P(0.05, 0.5), P(0.45, 0.5)], [P(0.25, 0.3), P(0.25, 0.7)]];
                return true;

            case 'm':
                width = 0.65;
                polylines = [
                    [P(0, 0), P(0, 1)],
                    [P(0, 0.75), P(0.12, 1), P(0.27, 0.75), P(0.27, 0)],
                    [P(0.27, 0.75), P(0.4, 1), P(0.55, 0.75), P(0.55, 0)]];
                return true;

            case 'c':
                width = 0.55;
                polylines = [[P(0.5, 0.8), P(0.3, 1), P(0.1, 0.9), P(0, 0.55), P(0.1, 0.2), P(0.3, 0.05), P(0.5, 0.15)]];
                return true;

            case 'i':
                width = 0.4;
                polylines = [[P(0.2, 0), P(0.2, 1)], [P(0.14, 1.22), P(0.26, 1.22)]];
                return true;

            case 'n':
                width = 0.6;
                polylines = [[P(0, 0), P(0, 1)], [P(0, 0.72), P(0.15, 1), P(0.4, 1), P(0.55, 0.8), P(0.55, 0)]];
                return true;

            default:
                width = 0;
                polylines = [];
                return false;
        }
    }
}
