namespace SmetaNA.Core.Inference;

/// <summary>
/// Тип привязки (inference). Точечные: <see cref="Origin"/>, <see cref="InstanceOrigin"/>,
/// <see cref="Endpoint"/>, <see cref="Midpoint"/>, <see cref="ArcMidpoint"/>, <see cref="Center"/>,
/// <see cref="Intersection"/>, <see cref="OnFace"/>, <see cref="OnEdge"/>, <see cref="Guide"/>,
/// <see cref="Grid"/>. Линейные ограничения (направления): <see cref="AxisX"/>,
/// <see cref="AxisY"/>, <see cref="AxisZ"/>, <see cref="AxisInstanceX"/>,
/// <see cref="Perpendicular"/>, <see cref="PerpendicularToFace"/>, <see cref="Parallel"/>,
/// <see cref="Tangent"/>, <see cref="TangentAtVertex"/>, <see cref="ExtendEdge"/>,
/// <see cref="FromPoint"/>. <see cref="GuidePoint"/> зарезервирован до v1.x.
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

    /// <summary>Точка на ребре: курсор скользит по ребру; несёт направление ребра для продолжения построения.</summary>
    OnEdge,

    /// <summary>Середина дуги (по длине дуги, а не хорды).</summary>
    ArcMidpoint,

    /// <summary>Начало координат и точка вставки группы/компонента.</summary>
    InstanceOrigin,

    /// <summary>Узел сетки привязки (наше расширение).</summary>
    Grid,

    /// <summary>Точка-направляющая (зарезервировано до v1.x: направляющие дают <see cref="Guide"/>).</summary>
    GuidePoint,

    /// <summary>Пользовательская точка сборки (v1.x).</summary>
    SnapPoint,

    /// <summary>Продолжение существующего ребра за его концы.</summary>
    ExtendEdge,

    /// <summary>Выравнивание от опорной/наведённой точки.</summary>
    FromPoint,

    /// <summary>Ось X активного контекста (группы/компонента).</summary>
    AxisInstanceX,

    /// <summary>Ось Y активного контекста.</summary>
    AxisInstanceY,

    /// <summary>Ось Z активного контекста.</summary>
    AxisInstanceZ,

    /// <summary>Перпендикуляр к наведённой грани.</summary>
    PerpendicularToFace,

    /// <summary>Касательная в вершине ранее нарисованной дуги.</summary>
    TangentAtVertex,
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
    OnEdges = 1 << 11,
    InstanceOrigins = 1 << 12,
    InstanceAxes = 1 << 13,
    Extensions = 1 << 14,
    Grid = 1 << 15,
    SnapPoints = 1 << 16,

    /// <summary>Только точечные привязки.</summary>
    Points = Origin | Endpoints | Midpoints | Centers | Intersections | OnFace | Guides
        | OnEdges | InstanceOrigins | Grid | SnapPoints,

    /// <summary>Только линейные ограничения.</summary>
    Linear = Axes | InstanceAxes | Extensions | Perpendicular | Parallel | Tangent,

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

    /// <summary>Только параллель и перпендикуляр (режим Alt в SketchUp).</summary>
    ParallelPerpendicularOnly = 1 << 7,
}

/// <summary>Режим линейных привязок, переключаемый Alt: все / только точки / только magenta.</summary>
public enum SnapInferenceMode
{
    All,
    PointsOnly,
    ParallelPerpendicularOnly,
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

    /// <summary>Крест — пересечение, узел сетки.</summary>
    Cross,

    /// <summary>Ромб — точка на грани и на направляющей.</summary>
    Diamond,

    /// <summary>Квадрат — точка на ребре.</summary>
    Square,

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
    /// <summary>
    /// Окно антимерцания (R-31): пока кандидат стал лучшим менее 150 мс назад,
    /// новый кандидат не перехватывает его без веской причины.
    /// </summary>
    public const double DwellMs = 150.0;

    /// <summary>Во сколько раз может отъехать курсор, чтобы «свежий» кандидат удержался.</summary>
    public const double DwellToleranceFactor = 1.5;

    /// <summary>Перехват удержанного кандидата, только если новый ближе на 2 px.</summary>
    public const double SwitchDistancePx = 2.0;

    /// <summary>
    /// Удержание над точкой (R-37): через сколько миллисекунд кандидат получает
    /// надбавку к приоритету.
    /// </summary>
    public const double DwellPriorityMs = 250.0;

    /// <summary>Надбавка к приоритету за удержание над точкой (R-37).</summary>
    public const int DwellPriorityBonus = 50;

    /// <summary>
    /// Насколько новый кандидат должен быть приоритетнее удержанного, чтобы
    /// перехватить привязку раньше окна антимерцания.
    /// </summary>
    public const int HysteresisPriorityDelta = 100;

    public const int SnapPointPriority = 950;    public const int EndpointPriority = 900;
    public const int InstanceOriginPriority = 860;
    public const int OriginPriority = 850;
    public const int IntersectionPriority = 800;
    public const int MidpointPriority = 700;
    public const int ArcMidpointPriority = 690;
    public const int CenterPriority = 600;
    public const int GuidePriority = 500;
    public const int OnEdgePriority = 450;
    public const int OnFacePriority = 400;
    public const int InstanceAxisPriority = 320;
    public const int AxisPriority = 300;
    public const int ExtendEdgePriority = 295;
    public const int FromPointPriority = 285;
    public const int PerpendicularPriority = 280;
    public const int PerpendicularToFacePriority = 275;
    public const int ParallelPriority = 260;
    public const int TangentPriority = 240;
    public const int TangentAtVertexPriority = 235;
    public const int GridPriority = 120;

    /// <summary>Приоритет типа по умолчанию (выше — раньше в списке кандидатов).</summary>
    public static int PriorityOf(SnapType type) => type switch
    {
        SnapType.SnapPoint => SnapPointPriority,
        SnapType.Endpoint => EndpointPriority,
        SnapType.InstanceOrigin => InstanceOriginPriority,
        SnapType.Origin => OriginPriority,
        SnapType.Intersection => IntersectionPriority,
        SnapType.Midpoint => MidpointPriority,
        SnapType.ArcMidpoint => ArcMidpointPriority,
        SnapType.Center => CenterPriority,
        SnapType.Guide or SnapType.GuidePoint => GuidePriority,
        SnapType.OnEdge => OnEdgePriority,
        SnapType.OnFace => OnFacePriority,
        SnapType.AxisInstanceX or SnapType.AxisInstanceY or SnapType.AxisInstanceZ => InstanceAxisPriority,
        SnapType.AxisX or SnapType.AxisY or SnapType.AxisZ => AxisPriority,
        SnapType.ExtendEdge => ExtendEdgePriority,
        SnapType.FromPoint => FromPointPriority,
        SnapType.Perpendicular => PerpendicularPriority,
        SnapType.PerpendicularToFace => PerpendicularToFacePriority,
        SnapType.Parallel => ParallelPriority,
        SnapType.Tangent => TangentPriority,
        SnapType.TangentAtVertex => TangentAtVertexPriority,
        SnapType.Grid => GridPriority,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип привязки."),
    };

    /// <summary>Категория фильтра, которой принадлежит тип.</summary>
    public static SnapFilter FilterOf(SnapType type) => type switch
    {
        SnapType.Origin => SnapFilter.Origin,
        SnapType.Endpoint => SnapFilter.Endpoints,
        SnapType.Midpoint or SnapType.ArcMidpoint => SnapFilter.Midpoints,
        SnapType.Center => SnapFilter.Centers,
        SnapType.Intersection => SnapFilter.Intersections,
        SnapType.OnFace => SnapFilter.OnFace,
        SnapType.Guide or SnapType.GuidePoint => SnapFilter.Guides,
        SnapType.OnEdge => SnapFilter.OnEdges,
        SnapType.InstanceOrigin => SnapFilter.InstanceOrigins,
        SnapType.Grid => SnapFilter.Grid,
        SnapType.SnapPoint => SnapFilter.SnapPoints,
        SnapType.AxisX or SnapType.AxisY or SnapType.AxisZ => SnapFilter.Axes,
        SnapType.AxisInstanceX or SnapType.AxisInstanceY or SnapType.AxisInstanceZ => SnapFilter.InstanceAxes,
        SnapType.ExtendEdge or SnapType.FromPoint => SnapFilter.Extensions,
        SnapType.Perpendicular or SnapType.PerpendicularToFace => SnapFilter.Perpendicular,
        SnapType.Parallel => SnapFilter.Parallel,
        SnapType.Tangent or SnapType.TangentAtVertex => SnapFilter.Tangent,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип привязки."),
    };

    /// <summary>Маркер типа по умолчанию.</summary>
    public static SnapMarkerKind MarkerOf(SnapType type) => type switch
    {
        SnapType.Origin or SnapType.InstanceOrigin or SnapType.SnapPoint => SnapMarkerKind.Ring,
        SnapType.Endpoint => SnapMarkerKind.Circle,
        SnapType.Midpoint or SnapType.ArcMidpoint or SnapType.Center
            or SnapType.Tangent or SnapType.TangentAtVertex => SnapMarkerKind.Circle,
        SnapType.Intersection or SnapType.Grid => SnapMarkerKind.Cross,
        SnapType.OnFace or SnapType.GuidePoint => SnapMarkerKind.Diamond,
        SnapType.Guide => SnapMarkerKind.Diamond,
        SnapType.OnEdge => SnapMarkerKind.Square,
        _ => SnapMarkerKind.Line,
    };
}
