using System.Globalization;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Annotations;

/// <summary>
/// Подпись размера в единицах проекта (<see cref="LengthUnit"/>): значение показывается
/// с точностью до 0,01 мм (DoD §5.4), десятичный разделитель — запятая (русская
/// чертёжная норма), обозначение единицы — латинское (mm/cm/m/in), как у разбора VCB.
/// </summary>
public static class DimensionText
{
    /// <summary>Обозначение единицы длины в подписи.</summary>
    public static string UnitSuffix(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => "mm",
        LengthUnit.Centimeter => "cm",
        LengthUnit.Meter => "m",
        LengthUnit.Inch => "in",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    /// <summary>Знаков после запятой, достаточных, чтобы показать 0,01 мм в единицах проекта.</summary>
    public static int Decimals(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => 2,
        LengthUnit.Centimeter => 3,
        LengthUnit.Meter => 5,
        LengthUnit.Inch => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    /// <summary>
    /// Подпись значения: мм → единицы проекта, округление half-away-from-zero до
    /// разрешающей способности 0,01 мм; <paramref name="textOverride"/> заменяет значение.
    /// </summary>
    public static string Format(double lengthMm, LengthUnit unit, string? textOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(textOverride))
        {
            return textOverride;
        }

        if (!double.IsFinite(lengthMm))
        {
            throw new ArgumentOutOfRangeException(nameof(lengthMm), lengthMm, "Длина должна быть конечной.");
        }

        int decimals = Decimals(unit);
        double value = Math.Abs(lengthMm) / unit.ToMillimeters();
        string number = Math.Round(value, decimals, MidpointRounding.AwayFromZero)
            .ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            .Replace('.', ',');
        return string.Concat(number, " ", UnitSuffix(unit));
    }
}
