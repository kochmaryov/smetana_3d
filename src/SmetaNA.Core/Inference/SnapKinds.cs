namespace SmetaNA.Core.Inference;

/// <summary>
/// Тип привязки (inference). Точечные: <see cref="Origin"/>, <see cref="Endpoint"/>,
/// <see cref="Midpoint"/>, <see cref="Center"/>, <see cref="Intersection"/>,
/// <see cref="OnFace"/>, <see cref="Guide"/>. Линейные ограничения (направления):
/// <see cref="AxisX"/>, <see cref="AxisY"/>, <see cref="AxisZ"/>,
/// <see cref="Perpendicular"/>, <see cref="Parallel"/>, <see cref="Tangent"/>.
/// </summary>
public enum SnapType
{
    /// <summary>Начало координат модели.</summary>
    Origin,

    /// <summary>Конец ребра.</summary>
    Endpoint,

    /// <summary>Середина ребра.</summary>
    Midpoint,

    /// <summary>Центр окружности или центроид грани.</summary>
    Center,

    /// <summary>Точка пересечения рёбер, направляющих и их комбинаций.</summary>
    Intersection,

    /// <summary>Точка на грани (проекция курсора на поверхность).</summary>
    OnFace,

    /// <summary>Точка на направляющей (линейке).</summary>
    Guide,

    /// <summary>Линейное ограничение: ось X (красная).</summary>
    AxisX,

    /// <summary>Линейное ограничение: ось Y (зелёная).</summary>
    AxisY,

    /// <summary>Линейное ограничение: ось Z (синяя).</summary>
    AxisZ,

    /// <summary>Линейное ограничение: перпендикуляр к ребру/направляющей из опорной точки.</summary>
    Perpendicular,

    /// <summary>Линейное ограничение: параллельно ребру/направляющей через опорную точку.</summary>
    Parallel,

    /// <summary>Линейное ограничение: касательная к окружности из опорной точки.</summary>
    Tangent,
}

/// <summary>Фильтры привязок: включение/выключение категорий.</summary>
[Flags]
public enum SnapFilter
{
    None = 0,
    Origin = 1 << 0,
    Endpoints = 1 << 1,
    Midpoints = 1 << 2,
    Centers = 1 << 3,
    Intersections = 1 << 4,
    OnFace = 1 << 5,
    Guides = 1 << 6,
    Axes = 1 << 7,
    Perpendicular = 1 << 8,
    Parallel = 1 << 9,
    Tangent = 1 << 10,

    /// <summary>Только точечные привязки.</summary>
    Points = Origin | Endpoints | Midpoints | Centers | Intersections | OnFace | Guides,

    /// <summary>Только линейные ограничения.</summary>
    Linear = Axes | Perpendicular | Parallel | Tangent,

    All = Points | Linear,
}

/// <summary>
/// Модификаторы запроса (клавиши): заморозка направления и отключение ограничений.
/// Любой lock-флаг переводит движок в «режим направления»: точечные привязки
/// подавляются, линейные ограничения строятся только перечисленных типов.
/// </summary>
[Flags]
public enum SnapModifiers
{
    None = 0,
    LockAxisX = 1 << 0,
    LockAxisY = 1 << 1,
    LockAxisZ = 1 << 2,
    LockParallel = 1 << 3,
    LockPerpendicular = 1 << 4,
    LockTangent = 1 << 5,

    /// <summary>Только точечные привязки, без линейных ограничений.</summary>
    PointsOnly = 1 << 6,
}

/// <summary>
/// Форма визуального маркера привязки (модель подсказки; отрисовка — на стороне UI).
/// Цвет/категорию маркер определяет <see cref="SnapType"/>.
/// </summary>
public enum SnapMarkerKind
{
    /// <summary>Кубик — конец ребра.</summary>
    Cube,

    /// <summary>Окружность — середина, центр, касательная.</summary>
    Circle,

    /// <summary>Крест — пересечение.</summary>
    Cross,

    /// <summary>Точка — точка на грани/направляющей.</summary>
    Dot,

    /// <summary>Кольцо — начало координат.</summary>
    Ring,

    /// <summary>Линия — линейное ограничение (ось, параллель, перпендикуляр).</summary>
    Line,

    /// <summary>Линия и точка — комбинированная привязка (ограничение через точку).</summary>
    LineAndPoint,
}

/// <summary>
/// Приоритеты и значения по умолчанию для типов привязок. Порядок кандидатов:
/// по убыванию <see cref="PriorityOf"/>, затем по убыванию уверенности, затем по глубине.
/// Комбинированная привязка (линейное ограничение через точечную) получает
/// приоритет своей точки + 1 и маркер <see cref="SnapMarkerKind.LineAndPoint"/>.
/// </summary>
public static class SnapDefaults
{
    public const int EndpointPriority = 900;
    public const int OriginPriority = 850;
    public const int IntersectionPriority = 800;
    public const int MidpointPriority = 700;
    public const int CenterPriority = 600;
    public const int GuidePriority = 500;
    public const int OnFacePriority = 400;
    public const int AxisPriority = 300;
    public const int PerpendicularPriority = 280;
    public const int ParallelPriority = 260;
    public const int TangentPriority = 240;

    /// <summary>Приоритет типа по умолчанию (выше — раньше в списке кандидатов).</summary>
    public static int PriorityOf(SnapType type) => type switch
    {
        SnapType.Origin => OriginPriority,
        SnapType.Endpoint => EndpointPriority,
        SnapType.Intersection => IntersectionPriority,
        SnapType.Midpoint => MidpointPriority,
        SnapType.Center => CenterPriority,
        SnapType.Guide => GuidePriority,
        SnapType.OnFace => OnFacePriority,
        SnapType.AxisX or SnapType.AxisY or SnapType.AxisZ => AxisPriority,
        SnapType.Perpendicular => PerpendicularPriority,
        SnapType.Parallel => ParallelPriority,
        SnapType.Tangent => TangentPriority,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип привязки."),
    };

    /// <summary>Категория фильтра, которой принадлежит тип.</summary>
    public static SnapFilter FilterOf(SnapType type) => type switch
    {
        SnapType.Origin => SnapFilter.Origin,
        SnapType.Endpoint => SnapFilter.Endpoints,
        SnapType.Midpoint => SnapFilter.Midpoints,
        SnapType.Center => SnapFilter.Centers,
        SnapType.Intersection => SnapFilter.Intersections,
        SnapType.OnFace => SnapFilter.OnFace,
        SnapType.Guide => SnapFilter.Guides,
        SnapType.AxisX or SnapType.AxisY or SnapType.AxisZ => SnapFilter.Axes,
        SnapType.Perpendicular => SnapFilter.Perpendicular,
        SnapType.Parallel => SnapFilter.Parallel,
        SnapType.Tangent => SnapFilter.Tangent,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип привязки."),
    };

    /// <summary>Маркер типа по умолчанию.</summary>
    public static SnapMarkerKind MarkerOf(SnapType type) => type switch
    {
        SnapType.Origin => SnapMarkerKind.Ring,
        SnapType.Endpoint => SnapMarkerKind.Cube,
        SnapType.Midpoint or SnapType.Center or SnapType.Tangent => SnapMarkerKind.Circle,
        SnapType.Intersection => SnapMarkerKind.Cross,
        SnapType.OnFace or SnapType.Guide => SnapMarkerKind.Dot,
        _ => SnapMarkerKind.Line,
    };
}
