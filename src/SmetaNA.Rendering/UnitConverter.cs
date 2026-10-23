using SmetaNA.Core.Model;

namespace SmetaNA.Rendering;

/// <summary>
/// Пересчёт длин: база сцены — миллиметры (DoD §5.4), единицы проекта — <see cref="LengthUnit"/>.
/// </summary>
public static class UnitConverter
{
    /// <summary>Миллиметры → единицы проекта.</summary>
    public static double ToUnits(double millimeters, LengthUnit unit) =>
        millimeters / unit.ToMillimeters();

    /// <summary>Единицы проекта → миллиметры.</summary>
    public static double ToMillimeters(double value, LengthUnit unit) =>
        value * unit.ToMillimeters();
}
