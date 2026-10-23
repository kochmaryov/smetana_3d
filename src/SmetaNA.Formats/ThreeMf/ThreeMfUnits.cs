using Lib3MF;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ThreeMf;

/// <summary>
/// Соответствие единиц SmetaNA и 3MF (модуль M8): 3MF описывает единицы сам
/// (атрибут unit модели), поэтому при экспорте единицы файла задаются явно,
/// а при импорте координаты нормализуются к миллиметрам по описанию файла.
/// 3MF знает шесть единиц; <see cref="LengthUnit"/> покрывает четыре из них,
/// микрометр и фут нормализуются по точному коэффициенту.
/// </summary>
internal static class ThreeMfUnits
{
    /// <summary>Единицы файла для экспорта.</summary>
    public static eModelUnit ToModelUnit(LengthUnit unit) => unit switch
    {
        LengthUnit.Millimeter => eModelUnit.MilliMeter,
        LengthUnit.Centimeter => eModelUnit.CentiMeter,
        LengthUnit.Meter => eModelUnit.Meter,
        LengthUnit.Inch => eModelUnit.Inch,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    /// <summary>Миллиметров в единице файла (все шесть единиц 3MF).</summary>
    public static double MillimetersPerUnit(eModelUnit unit) => unit switch
    {
        eModelUnit.MicroMeter => 0.001,
        eModelUnit.MilliMeter => 1.0,
        eModelUnit.CentiMeter => 10.0,
        eModelUnit.Inch => 25.4,
        eModelUnit.Foot => 304.8,
        eModelUnit.Meter => 1000.0,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    /// <summary>
    /// Представление единиц 3MF в <see cref="LengthUnit"/>; false — для микрометра
    /// и фута, которых нет в <see cref="LengthUnit"/> (их имя в отчёт передаётся явно).
    /// </summary>
    public static bool TryGetLengthUnit(eModelUnit unit, out LengthUnit lengthUnit)
    {
        switch (unit)
        {
            case eModelUnit.MilliMeter:
                lengthUnit = LengthUnit.Millimeter;
                return true;
            case eModelUnit.CentiMeter:
                lengthUnit = LengthUnit.Centimeter;
                return true;
            case eModelUnit.Meter:
                lengthUnit = LengthUnit.Meter;
                return true;
            case eModelUnit.Inch:
                lengthUnit = LengthUnit.Inch;
                return true;
            default:
                lengthUnit = LengthUnit.Millimeter;
                return false;
        }
    }
}
