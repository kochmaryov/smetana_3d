using System.Globalization;

namespace SmetaNA.Formats;

/// <summary>
/// Детерминированный формат чисел для текстовых форматов: одна и та же геометрия
/// даёт байт-в-байт одинаковые файлы (требование байтовой стабильности).
/// </summary>
internal static class TextFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Число STL (одиночная точность): R — кратчайшее представление, точно читаемое обратно в float32.</summary>
    public static string Stl(float value) => value.ToString("R", Invariant);

    /// <summary>Число OBJ (двойная точность): 6 знаков после запятой (нм при базе в мм).</summary>
    public static string Obj(double value) => value.ToString("0.######", Invariant);
}
