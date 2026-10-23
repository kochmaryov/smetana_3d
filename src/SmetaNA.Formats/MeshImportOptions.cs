using SmetaNA.Core.Model;

namespace SmetaNA.Formats;

/// <summary>Опции импорта меша.</summary>
/// <param name="Unit">
/// Единицы чисел в файле; координаты приводятся к миллиметрам
/// (внутренняя база редактора — мм, DoD §5.4).
/// </param>
/// <param name="WeldVertices">
/// Сварка одинаковых позиций при разборе треугольного супа (STL)
/// или восстановлении индексов (OBJ). Отключение даёт невареный суп —
/// топология <c>HalfEdgeMesh</c> для него не строится.
/// </param>
public sealed record MeshImportOptions(LengthUnit Unit = LengthUnit.Millimeter, bool WeldVertices = true);
