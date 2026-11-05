using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Отрезок (ребро) снимка сцены для привязок: концы, середина, направление.
/// </summary>
public readonly record struct SnapSegment
{
    public Vector3d Start { get; }
    public Vector3d End { get; }

    /// <summary>Источник ребра в снимке (для подсветки и подписи).</summary>
    public SnapEntityRef Entity { get; init; }

    public SnapSegment(Vector3d start, Vector3d end)
    {
        if (!InferenceMath.IsFinite(start) || !InferenceMath.IsFinite(end))
        {
            throw new ArgumentException("Концы отрезка должны быть конечными.");
        }

        if ((end - start).LengthSquared() <= 0)
        {
            throw new ArgumentException("Отрезок не может быть вырожденным.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public double Length => (End - Start).Length();

    public Vector3d Midpoint => (Start + End) * 0.5;

    /// <summary>Направление от <see cref="Start"/> к <see cref="End"/>, единичное.</summary>
    public Vector3d Direction => (End - Start).Normalized();

    /// <summary>Ближайшая точка отрезка (с ограничением по концам).</summary>
    public Vector3d ClosestPoint(Vector3d point)
    {
        Vector3d direction = End - Start;
        double lengthSquared = direction.LengthSquared();
        double t = lengthSquared <= 0 ? 0 : Vector3d.Dot(point - Start, direction) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        return Start + direction * t;
    }
}

/// <summary>
/// Грань снимка сцены: планарный многоугольник (кольцо вершин, минимум 3)
/// с единичной нормалью Ньюэлла и центроидом.
/// </summary>
public sealed class SnapFace
{
    private readonly Vector3d[] _ring;

    public SnapFace(IReadOnlyList<Vector3d> ring)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Count < 3)
        {
            throw new ArgumentException("Грань требует минимум трёх вершин.", nameof(ring));
        }

        _ring = new Vector3d[ring.Count];
        var sum = Vector3d.Zero;
        for (int i = 0; i < ring.Count; i++)
        {
            if (!InferenceMath.IsFinite(ring[i]))
            {
                throw new ArgumentException("Вершины грани должны быть конечными.", nameof(ring));
            }

            _ring[i] = ring[i];
            sum += ring[i];
        }

        Normal = ComputeNewellNormal(_ring);
        if (Normal.LengthSquared() <= 0)
        {
            throw new ArgumentException("Грань вырождена (нулевая нормаль).", nameof(ring));
        }

        Centroid = sum * (1.0 / ring.Count);

        double radiusSquared = 0;
        for (int i = 0; i < _ring.Length; i++)
        {
            radiusSquared = Math.Max(radiusSquared, (_ring[i] - Centroid).LengthSquared());
        }

        BoundingRadius = Math.Sqrt(radiusSquared);
    }

    public IReadOnlyList<Vector3d> Ring => _ring;

    /// <summary>Единичная нормаль грани (правая рука по обходу кольца).</summary>
    public Vector3d Normal { get; }

    /// <summary>Центроид вершин кольца.</summary>
    public Vector3d Centroid { get; }

    /// <summary>
    /// Радиус описанной сферы контура вокруг центроида: точка вне сферы заведомо
    /// не принадлежит грани — дешёвый отсев перед дорогой проверкой принадлежности.
    /// </summary>
    public double BoundingRadius { get; }

    /// <summary>Источник грани в снимке (для подсветки и подписи).</summary>
    public SnapEntityRef Entity { get; init; }

    /// <summary>
    /// Точка пересечения луча с плоскостью грани; <c>false</c> — луч
    /// параллелен плоскости.
    /// </summary>
    public bool TryIntersect(Ray3d ray, out Vector3d point)
    {
        point = default;
        double denominator = Vector3d.Dot(ray.Direction, Normal);
        if (Math.Abs(denominator) <= 1e-12)
        {
            return false;
        }

        double t = Vector3d.Dot(_ring[0] - ray.Origin, Normal) / denominator;
        point = ray.PointAt(t);
        return true;
    }

    /// <summary>Принадлежность точки грани (с допуском границы в мировых единицах).</summary>
    public bool Contains(Vector3d point, double tolerance)
        => InferenceMath.PointInRing(_ring, Normal, point, tolerance);

    private static Vector3d ComputeNewellNormal(Vector3d[] ring)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < ring.Length; i++)
        {
            Vector3d current = ring[i];
            Vector3d next = ring[(i + 1) % ring.Length];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z).Normalized();
    }
}

/// <summary>
/// Окружность снимка сцены (центр, единичная нормаль плоскости, радиус) —
/// источник привязок центра и касательных.
/// </summary>
public readonly record struct SnapCircle
{
    public Vector3d Center { get; }
    public Vector3d Normal { get; }
    public double Radius { get; }

    public SnapCircle(Vector3d center, Vector3d normal, double radius)
    {
        if (!InferenceMath.IsFinite(center))
        {
            throw new ArgumentException("Центр окружности должен быть конечным.", nameof(center));
        }

        if (!InferenceMath.IsFinite(normal) || normal.LengthSquared() <= 0)
        {
            throw new ArgumentException("Нормаль окружности должна быть ненулевой.", nameof(normal));
        }

        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Радиус окружности должен быть положительным.");
        }

        Center = center;
        Normal = normal.Normalized();
        Radius = radius;
    }

    /// <summary>Источник окружности в снимке (для подсветки и подписи).</summary>
    public SnapEntityRef Entity { get; init; }
}

/// <summary>Середина открытой дуги: точка на кривой, а не центр окружности.</summary>
public readonly record struct SnapArcPoint(Vector3d Point, SnapEntityRef Entity);

/// <summary>
/// Касательная в концевой вершине дуги (R-34): точка вершины и направление
/// касательной дуги в ней.
/// </summary>
public readonly record struct SnapArcTangent(Vector3d Point, Vector3d Direction, SnapEntityRef Entity);

/// <summary>
/// Пользовательская точка сборки (Snap) в мировых координатах: позиция, нормаль
/// диска и up. Привязка к ней даёт ориентацию для соединения «лицевая к лицевой».
/// </summary>
public readonly record struct SnapUserPoint(SnapFrame Frame, SnapEntityRef Entity);

/// <summary>
/// Начало координат группы или инстанса компонента (Component Origin Point)
/// с мировыми осями контекста.
/// </summary>
public sealed class SnapInstanceOrigin
{
    public SnapInstanceOrigin(
        Vector3d origin,
        Vector3d axisX,
        Vector3d axisY,
        Vector3d axisZ,
        SnapEntityRef entity)
    {
        if (!InferenceMath.IsFinite(origin))
        {
            throw new ArgumentException("Начало координат контекста должно быть конечным.", nameof(origin));
        }

        if (!IsUsableAxis(axisX) || !IsUsableAxis(axisY) || !IsUsableAxis(axisZ))
        {
            throw new ArgumentException("Оси контекста должны быть ненулевыми и конечными.", nameof(axisX));
        }

        Origin = origin;
        AxisX = axisX.Normalized();
        AxisY = axisY.Normalized();
        AxisZ = axisZ.Normalized();
        Entity = entity;
    }

    public Vector3d Origin { get; }
    public Vector3d AxisX { get; }
    public Vector3d AxisY { get; }
    public Vector3d AxisZ { get; }
    public SnapEntityRef Entity { get; }

    private static bool IsUsableAxis(Vector3d axis)
        => InferenceMath.IsFinite(axis) && axis.LengthSquared() > 0;
}

/// <summary>
/// Снимок состояния сцены для inference: мировые рёбра, грани и окружности.
/// Движок работает только со снимком, поэтому полностью тестируем без UI.
/// </summary>
public sealed class InferenceScene
{
    /// <summary>Пустой снимок: привязки только к началу координат и направляющим.</summary>
    public static readonly InferenceScene Empty = new([], [], []);

    private readonly SnapSegment[] _segments;
    private readonly SnapFace[] _faces;
    private readonly SnapCircle[] _circles;
    private readonly SnapArcPoint[] _arcPoints;
    private readonly SnapArcTangent[] _arcTangents;
    private readonly SnapInstanceOrigin[] _instanceOrigins;
    private readonly SnapUserPoint[] _userSnaps;

    public InferenceScene(
        IEnumerable<SnapSegment> segments,
        IEnumerable<SnapFace> faces,
        IEnumerable<SnapCircle>? circles = null,
        IEnumerable<SnapArcPoint>? arcPoints = null,
        IEnumerable<SnapInstanceOrigin>? instanceOrigins = null,
        IEnumerable<SnapUserPoint>? userSnaps = null,
        IEnumerable<SnapArcTangent>? arcTangents = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(faces);

        _segments = [.. segments];
        _faces = [.. faces];
        _circles = circles is null ? [] : [.. circles];
        _arcPoints = arcPoints is null ? [] : [.. arcPoints];
        _arcTangents = arcTangents is null ? [] : [.. arcTangents];
        _instanceOrigins = instanceOrigins is null ? [] : [.. instanceOrigins];
        _userSnaps = userSnaps is null ? [] : [.. userSnaps];

        if (_faces.Any(f => f is null))
        {
            throw new ArgumentException("Снимок не может содержать пустые грани.", nameof(faces));
        }

        if (_instanceOrigins.Any(o => o is null))
        {
            throw new ArgumentException("Снимок не может содержать пустые начала координат.", nameof(instanceOrigins));
        }
    }

    public IReadOnlyList<SnapSegment> Segments => _segments;

    /// <summary>
    /// Массив сегментов для горячих циклов движка: обращение через интерфейс
    /// <see cref="Segments"/> на сотнях тысяч рёбер дороже индексации массива.
    /// </summary>
    internal SnapSegment[] SegmentsArray => _segments;
    public IReadOnlyList<SnapFace> Faces => _faces;
    public IReadOnlyList<SnapCircle> Circles => _circles;

    /// <summary>Середины открытых дуг проволочных цепей.</summary>
    public IReadOnlyList<SnapArcPoint> ArcPoints => _arcPoints;

    /// <summary>Касательные в концевых вершинах дуг.</summary>
    public IReadOnlyList<SnapArcTangent> ArcTangents => _arcTangents;

    /// <summary>Начала координат групп и инстансов компонентов.</summary>
    public IReadOnlyList<SnapInstanceOrigin> InstanceOrigins => _instanceOrigins;

    /// <summary>Пользовательские точки сборки (Snap) в мировых координатах.</summary>
    public IReadOnlyList<SnapUserPoint> UserSnaps => _userSnaps;

    /// <summary>
    /// Копия снимка с добавленными сущностями строящейся фигуры (рёбра, окружности).
    /// Середины дуг, их касательные и начала координат контейнеров сохраняются — новый
    /// <see cref="InferenceScene"/> собирайте только через этот метод.
    /// </summary>
    public InferenceScene WithExtra(
        IEnumerable<SnapSegment>? extraSegments = null,
        IEnumerable<SnapCircle>? extraCircles = null)
    {
        List<SnapSegment> segments = [.. _segments];
        if (extraSegments is not null)
        {
            segments.AddRange(extraSegments);
        }

        List<SnapCircle> circles = [.. _circles];
        if (extraCircles is not null)
        {
            circles.AddRange(extraCircles);
        }

        return new InferenceScene(
            segments,
            _faces,
            circles,
            _arcPoints,
            _instanceOrigins,
            _userSnaps,
            _arcTangents);
    }
}
