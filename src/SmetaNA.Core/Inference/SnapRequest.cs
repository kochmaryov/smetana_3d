using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Запрос привязок: луч выбора (курсор через камеру), состояние сцены и направляющие,
/// модификаторы (клавиши) и толеранс в пикселях экрана.
/// <para>
/// <see cref="Ray"/> и <see cref="CursorPx"/> описывают одно положение курсора:
/// <c>Viewport.PickRay(CursorPx)</c> даёт тот же луч (см. <see cref="AtCursor"/>).
/// </para>
/// </summary>
public sealed record SnapRequest
{
    /// <summary>Радиус толеранса по умолчанию, пикселей.</summary>
    public const double DefaultTolerancePx = 8.0;

    /// <summary>Луч выбора от камеры через курсор.</summary>
    public required Ray3d Ray { get; init; }

    /// <summary>Параметры камеры и вьюпорта: проекция и перевод толеранса в мир.</summary>
    public required InferenceViewport Viewport { get; init; }

    /// <summary>Состояние сцены (снимок геометрии в мировых координатах).</summary>
    public required InferenceScene Scene { get; init; }

    /// <summary>Положение курсора в пикселях (отсчёт от левого верхнего угла).</summary>
    public required Vector2d CursorPx { get; init; }

    /// <summary>Радиус толеранса привязки в пикселях экрана.</summary>
    public double TolerancePx { get; init; } = DefaultTolerancePx;

    /// <summary>
    /// Опорная точка текущего построения (откуда инструмент тянет отрезок);
    /// без неё линейные ограничения строятся через начало координат, а
    /// перпендикуляры/касательные не строятся вовсе.
    /// </summary>
    public Vector3d? Anchor { get; init; }

    /// <summary>Фильтр категорий привязок.</summary>
    public SnapFilter Filter { get; init; } = SnapFilter.All;

    /// <summary>Модификаторы (клавиши): заморозка направления, только точечные привязки.</summary>
    public SnapModifiers Modifiers { get; init; } = SnapModifiers.None;

    /// <summary>Направляющие (линейки/транспортир), доступные для привязок.</summary>
    public IReadOnlyList<SnapGuide> Guides { get; init; } = Array.Empty<SnapGuide>();

    /// <summary>
    /// Последняя наведённая/зафиксированная точка (не начало построения) — источник
    /// выравнивания <see cref="SnapType.FromPoint"/>. <c>null</c> — выравнивание от
    /// точки не строится.
    /// </summary>
    public Vector3d? ReferencePoint { get; init; }

    /// <summary>Сетка привязки; <c>null</c> или выключенная — узлы сетки не предлагаются.</summary>
    public SnapGrid? Grid { get; init; }

    /// <summary>
    /// Зафиксированная привязка (Shift): движок возвращает только её — точку как есть,
    /// а направление «скользит» по зафиксированной линии за курсором.
    /// </summary>
    public SnapCandidate? LockedCandidate { get; init; }

    /// <summary>
    /// Запрос по пиксельному положению курсора: луч строится через
    /// <see cref="InferenceViewport.PickRay"/>.
    /// </summary>
    public static SnapRequest AtCursor(
        InferenceViewport viewport,
        InferenceScene scene,
        Vector2d cursorPx,
        double tolerancePx = DefaultTolerancePx)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(scene);

        return new SnapRequest
        {
            Ray = viewport.PickRay(cursorPx),
            Viewport = viewport,
            Scene = scene,
            CursorPx = cursorPx,
            TolerancePx = tolerancePx,
        };
    }
}
