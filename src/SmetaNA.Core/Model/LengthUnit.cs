namespace SmetaNA.Core.Model;

/// <summary>Единицы длины проекта (спека §3.2, модуль M4).</summary>
public enum LengthUnit
{
    Millimeter,
    Centimeter,
    Meter,
    Inch,
}

public static class LengthUnitExtensions
{
    /// <summary>Миллиметров в одной единице: база точности и экспорта (M8).</summary>
    public static double ToMillimeters(this LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => 1.0,
        LengthUnit.Centimeter => 10.0,
        LengthUnit.Meter => 1000.0,
        LengthUnit.Inch => 25.4,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };
}
