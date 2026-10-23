using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Отрезок (ребро) снимка сцены для привязок: концы, середина, направление.
/// </summary>
public readonly record struct SnapSegment
{
    public Vector3d Start { get; }
    public Vector3d End { get; }

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
    }

    public IReadOnlyList<Vector3d> Ring => _ring;

    /// <summary>Единичная нормаль грани (правая рука по обходу кольца).</summary>
    public Vector3d Normal { get; }

    /// <summary>Центроид вершин кольца.</summary>
    public Vector3d Centroid { get; }

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

    public InferenceScene(
        IEnumerable<SnapSegment> segments,
        IEnumerable<SnapFace> faces,
        IEnumerable<SnapCircle>? circles = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(faces);

        _segments = [.. segments];
        _faces = [.. faces];
        _circles = circles is null ? [] : [.. circles];

        if (_faces.Any(f => f is null))
        {
            throw new ArgumentException("Снимок не может содержать пустые грани.", nameof(faces));
        }
    }

    public IReadOnlyList<SnapSegment> Segments => _segments;
    public IReadOnlyList<SnapFace> Faces => _faces;
    public IReadOnlyList<SnapCircle> Circles => _circles;
}
