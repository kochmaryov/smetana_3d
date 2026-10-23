using SmetaNA.Core.Model;

namespace SmetaNA.Formats;

/// <summary>Отчёт импорта: что перенесено, что потеряно и какие дефекты найдены.</summary>
/// <param name="Format">Фактически распознанный формат файла.</param>
/// <param name="SourceUnit">Единицы чисел, заданные для файла при импорте.</param>
/// <param name="MillimetersPerUnit">Применённый масштаб: миллиметров в единице файла.</param>
/// <param name="SourceVertexCount">Записей вершин в файле (до сварки).</param>
/// <param name="VertexCount">Вершин в результате (после сварки).</param>
/// <param name="TriangleCount">Треугольников в результате (полигоны OBJ триангулированы веером).</param>
/// <param name="DegenerateTriangleIndices">Индексы вырожденных треугольников (нулевая площадь или повтор вершин).</param>
/// <param name="LostFeatures">Что не переносится форматом/импортом (материалы, группы, имена и т.п.).</param>
/// <param name="Warnings">Предупреждения разбора и нормализации.</param>
public sealed record MeshImportReport(
    MeshFileFormat Format,
    LengthUnit SourceUnit,
    double MillimetersPerUnit,
    int SourceVertexCount,
    int VertexCount,
    int TriangleCount,
    IReadOnlyList<int> DegenerateTriangleIndices,
    IReadOnlyList<string> LostFeatures,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Вырожденных треугольников.</summary>
    public int DegenerateTriangleCount => DegenerateTriangleIndices.Count;

    /// <summary>Сколько записей вершин поглощено сваркой.</summary>
    public int WeldedVertexCount => SourceVertexCount - VertexCount;

    /// <summary>Человекочитаемый отчёт (RU), например для UI/лога.</summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            $"Формат: {Format}",
            $"Единицы файла: {UnitConversion.GetName(SourceUnit)} ({UnitConversion.MillimetersPerUnit(SourceUnit)} мм в единице)",
            $"Вершин: {VertexCount} (записей в файле: {SourceVertexCount}, сварено: {WeldedVertexCount})",
            $"Треугольников: {TriangleCount}",
            $"Вырожденных треугольников: {DegenerateTriangleCount}",
        };

        lines.Add("Не переносится:");
        if (LostFeatures.Count == 0)
        {
            lines.Add("  — нет");
        }
        else
        {
            foreach (string feature in LostFeatures)
            {
                lines.Add($"  — {feature}");
            }
        }

        if (Warnings.Count > 0)
        {
            lines.Add("Предупреждения:");
            foreach (string warning in Warnings)
            {
                lines.Add($"  — {warning}");
            }
        }

        return string.Join('\n', lines);
    }
}
