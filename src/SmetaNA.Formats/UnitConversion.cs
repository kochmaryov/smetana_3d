using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats;

/// <summary>
/// Контроль единиц обмена (спека M8): внутренняя база редактора — миллиметры,
/// форматы STL/OBJ безразмерны, поэтому единицы файла задаются явно.
/// </summary>
public static class UnitConversion
{
    /// <summary>Сколько миллиметров в одной единице файла.</summary>
    public static double MillimetersPerUnit(LengthUnit unit) => unit.ToMillimeters();

    /// <summary>Перевод координат файла в миллиметры.</summary>
    public static Vector3d ToMillimeters(Vector3d value, LengthUnit unit) => value * unit.ToMillimeters();

    /// <summary>Перевод миллиметров в единицы файла (масштаб экспорта).</summary>
    public static Vector3d FromMillimeters(Vector3d value, LengthUnit unit) => value * (1.0 / unit.ToMillimeters());

    /// <summary>Имя единиц для заголовков файлов: детерминированно, без локали.</summary>
    public static string GetName(LengthUnit unit) => unit.ToString();
}
