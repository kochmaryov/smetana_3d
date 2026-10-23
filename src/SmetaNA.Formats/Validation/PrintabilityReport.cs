namespace SmetaNA.Formats.Validation;

/// <summary>Статус отдельной проверки печатаемости.</summary>
public enum CheckStatus
{
    /// <summary>Проверка пройдена.</summary>
    Passed,

    /// <summary>Проверка не пройдена — дефект в <see cref="PrintabilityReport.Issues"/>.</summary>
    Failed,

    /// <summary>Проверка неприменима (например, пустой меш).</summary>
    NotApplicable,

    /// <summary>Проверка не реализована (задел) — см. заметки отчёта.</summary>
    NotImplemented,
}

/// <summary>Код дефекта печатаемости.</summary>
public enum PrintabilityIssueCode
{
    /// <summary>Незамкнутый контур: рёбра с одной инцидентной гранью.</summary>
    BoundaryEdges,

    /// <summary>Не 2-манифолден: ребро принадлежит более чем двум граням.</summary>
    NonManifoldEdges,

    /// <summary>Рассогласование ориентации нормалей: общее ребро обходится в одну сторону.</summary>
    InconsistentOrientation,

    /// <summary>Нормали направлены внутрь (инвертированный меш, замкнут).</summary>
    InvertedOrientation,

    /// <summary>Вырожденные треугольники (нулевая площадь или повтор вершин).</summary>
    DegenerateTriangles,

    /// <summary>Меш не содержит треугольников.</summary>
    EmptyMesh,

    /// <summary>Топология HalfEdgeMesh не строится (меш вырожден/не 2-манифолден).</summary>
    TopologyBuildFailed,
}

/// <summary>Важность дефекта.</summary>
public enum IssueSeverity
{
    /// <summary>Информация.</summary>
    Info,

    /// <summary>Предупреждение: дефект зачищается или переносится слайсером, но модель помечена.</summary>
    Warning,

    /// <summary>Ошибка: модель непечатаема без починки.</summary>
    Error,
}

/// <summary>Структурированный дефект печатаемости.</summary>
/// <param name="Code">Код дефекта.</param>
/// <param name="Severity">Важность.</param>
/// <param name="Count">Сколько объектов затронуто (граней, рёбер, треугольников).</param>
/// <param name="Message">Человекочитаемое описание (RU).</param>
public sealed record PrintabilityIssue(PrintabilityIssueCode Code, IssueSeverity Severity, int Count, string Message);

/// <summary>
/// Структурированный отчёт печатаемости (задача 4.4, M8).
/// <see cref="IsPrintable"/> = все обязательные проверки пройдены;
/// самопересечения в этой версии не анализируются (задел) и на готовность не влияют.
/// </summary>
public sealed record PrintabilityReport(
    bool IsPrintable,
    CheckStatus Watertight,
    CheckStatus NormalOrientation,
    CheckStatus DegenerateGeometry,
    CheckStatus SelfIntersection,
    int TriangleCount,
    int VertexCount,
    int BoundaryEdgeCount,
    int NonManifoldEdgeCount,
    int DuplicateDirectedEdgeCount,
    int DegenerateTriangleCount,
    bool? IsOutwardOriented,
    double SignedVolumeMm3,
    IReadOnlyList<PrintabilityIssue> Issues,
    IReadOnlyList<string> Notes)
{
    /// <summary>Человекочитаемый отчёт (RU), например для UI/лога.</summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            $"Печатаемость: {(IsPrintable ? "ГОТОВА К ПЕЧАТИ" : "НЕ ГОТОВА К ПЕЧАТИ")}",
            $"Треугольников: {TriangleCount}, вершин: {VertexCount}",
            $"Замкнутость (watertight): {Describe(Watertight)}; рёбер-границ: {BoundaryEdgeCount}, не 2-манифолдных: {NonManifoldEdgeCount}",
            $"Ориентация нормалей: {Describe(NormalOrientation)}; нормали наружу: {(IsOutwardOriented is null ? "не определено" : IsOutwardOriented.Value ? "да" : "нет")}",
            $"Вырожденные треугольники: {Describe(DegenerateGeometry)}; найдено: {DegenerateTriangleCount}",
            $"Самопересечения: {Describe(SelfIntersection)}",
            $"Ориентированный объём: {SignedVolumeMm3} мм³",
        };

        if (Issues.Count > 0)
        {
            lines.Add("Дефекты:");
            foreach (PrintabilityIssue issue in Issues)
            {
                lines.Add($"  — [{issue.Severity}] {issue.Message} (затронуто: {issue.Count})");
            }
        }

        if (Notes.Count > 0)
        {
            lines.Add("Заметки:");
            foreach (string note in Notes)
            {
                lines.Add($"  — {note}");
            }
        }

        return string.Join('\n', lines);
    }

    private static string Describe(CheckStatus status) => status switch
    {
        CheckStatus.Passed => "пройдена",
        CheckStatus.Failed => "НЕ пройдена",
        CheckStatus.NotApplicable => "не применима",
        _ => "не реализована (задел)",
    };
}
