using System.Globalization;
using System.Text.RegularExpressions;
using SmetaNA.Core.Model;

namespace SmetaNA.EditorUI.Vcb;

/// <summary>
/// Разбор и валидация числового ввода VCB (спека §3.2 п. 3):
/// длина/радиус/угол/координаты, единицы измерения, точка и запятая как разделитель дробной части.
/// </summary>
public static partial class VcbValueParser
{
    [GeneratedRegex(@"^(?<number>[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?)(?<suffix>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberWithSuffixRegex();

    /// <summary>Разбор скалярного значения: длина, радиус или угол.</summary>
    public static VcbParseResult ParseScalar(string? text, VcbInputKind kind, LengthUnit unit)
    {
        if (kind == VcbInputKind.Coordinate)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Для координат используйте ParseCoordinate.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return VcbParseResult.Fail(VcbError.Empty);
        }

        Match match = NumberWithSuffixRegex().Match(text.Trim());
        if (!match.Success)
        {
            return VcbParseResult.Fail(VcbError.InvalidNumber);
        }

        string suffix = match.Groups["suffix"].Value.Trim();
        if (LooksLikeAnotherNumber(suffix))
        {
            return VcbParseResult.Fail(VcbError.InvalidNumber);
        }

        if (!TryParseNumber(match.Groups["number"].Value, out double value))
        {
            return VcbParseResult.Fail(VcbError.InvalidNumber);
        }

        if (!IsFinite(value))
        {
            return VcbParseResult.Fail(VcbError.NotFinite);
        }

        if (kind == VcbInputKind.Angle)
        {
            if (!TryResolveAngleSuffix(suffix))
            {
                return VcbParseResult.Fail(VcbError.UnknownUnit);
            }

            return VcbParseResult.ForAngle(value);
        }

        if (!TryResolveLengthSuffix(suffix, unit, out double scaleToMillimeters))
        {
            return VcbParseResult.Fail(VcbError.UnknownUnit);
        }

        if (value < 0)
        {
            return VcbParseResult.Fail(VcbError.Negative);
        }

        if (kind == VcbInputKind.Radius && value == 0)
        {
            return VcbParseResult.Fail(VcbError.NotPositive);
        }

        return VcbParseResult.ForLength(value, value * scaleToMillimeters);
    }

    /// <summary>
    /// Разбор точки «x; y; z»: разделители — «;» или пробелы, у каждой координаты
    /// может быть своя единица; результат — в миллиметрах.
    /// </summary>
    public static VcbCoordinateResult ParseCoordinate(string? text, LengthUnit unit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return VcbCoordinateResult.Fail(VcbError.Empty);
        }

        string trimmed = text.Trim();
        string[] components = trimmed.Contains(';')
            ? trimmed.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : trimmed.Split([' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (components.Length != 3)
        {
            return VcbCoordinateResult.Fail(VcbError.ComponentCount);
        }

        var millimeters = new double[3];
        for (int i = 0; i < components.Length; i++)
        {
            VcbParseResult parsed = ParseScalar(components[i], VcbInputKind.Length, unit);
            if (!parsed.IsSuccess)
            {
                return VcbCoordinateResult.Fail(parsed.Error);
            }

            millimeters[i] = parsed.ValueInMillimeters!.Value;
        }

        return VcbCoordinateResult.Ok(millimeters[0], millimeters[1], millimeters[2]);
    }

    /// <summary>Каноническое отображение числа (локаль текущего потока).</summary>
    public static string FormatValue(double value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    private static bool LooksLikeAnotherNumber(string suffix) =>
        suffix.Length > 0 && (char.IsDigit(suffix[0]) || suffix[0] is '+' or '-' or '.' or ',');

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool TryResolveAngleSuffix(string suffix) =>
        suffix.Length == 0 || suffix is "°" or "deg" or "град" or "градусов";

    private static bool TryResolveLengthSuffix(string suffix, LengthUnit defaultUnit, out double scaleToMillimeters)
    {
        if (suffix.Length == 0)
        {
            scaleToMillimeters = defaultUnit.ToMillimeters();
            return true;
        }

        LengthUnit resolved;
        switch (suffix.ToLowerInvariant())
        {
            case "mm":
            case "мм":
                resolved = LengthUnit.Millimeter;
                break;
            case "cm":
            case "см":
                resolved = LengthUnit.Centimeter;
                break;
            case "m":
            case "м":
                resolved = LengthUnit.Meter;
                break;
            case "in":
            case "inch":
            case "дюйм":
            case "дюйма":
            case "\"":
            case "″":
                resolved = LengthUnit.Inch;
                break;
            default:
                scaleToMillimeters = 0;
                return false;
        }

        scaleToMillimeters = resolved.ToMillimeters();
        return true;
    }
}
