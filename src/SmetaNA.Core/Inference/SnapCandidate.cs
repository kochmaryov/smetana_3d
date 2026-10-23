using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Кандидат привязки: тип, мировая точка и (для линейных ограничений) направление.
/// Комбинированные привязки несут и точку, и направление, и вторичную точку.
/// </summary>
/// <param name="Type">Тип привязки.</param>
/// <param name="Point">Мировая точка привязки.</param>
/// <param name="Confidence">Уверенность 0..1 (ближе к курсору — выше).</param>
/// <param name="Priority">Приоритет: выше — раньше в списке (<see cref="SnapDefaults.PriorityOf"/>).</param>
/// <param name="Marker">Форма визуального маркера (UI рисует сам).</param>
public sealed record SnapCandidate(
    SnapType Type,
    Vector3d Point,
    double Confidence,
    int Priority,
    SnapMarkerKind Marker)
{
    /// <summary>
    /// Единичное направление линейного ограничения — вдоль него инструмент продолжит
    /// построение; <c>null</c> для чисто точечных привязок.
    /// </summary>
    public Vector3d? Direction { get; init; }

    /// <summary>
    /// Вторичная (опорная) точка ограничения — начало построения (якорь), из которого
    /// действует ограничение; <c>null</c> для точечных привязок и оси без якоря.
    /// </summary>
    public Vector3d? SecondaryPoint { get; init; }

    /// <summary>Расстояние от курсора до проекции <see cref="Point"/> в пикселях.</summary>
    public double ScreenDistancePx { get; init; }

    /// <summary>Комбинированная привязка: точка + линейное ограничение.</summary>
    public bool IsCombined => Direction is not null && Marker == SnapMarkerKind.LineAndPoint;
}

/// <summary>Результат запроса привязок: упорядоченные кандидаты.</summary>
public sealed record SnapResult(IReadOnlyList<SnapCandidate> Candidates)
{
    public static readonly SnapResult Empty = new(Array.Empty<SnapCandidate>());

    public bool IsEmpty => Candidates.Count == 0;

    /// <summary>Лучший кандидат (первый в списке) или <c>null</c>, если привязок нет.</summary>
    public SnapCandidate? Best => Candidates.Count > 0 ? Candidates[0] : null;
}
