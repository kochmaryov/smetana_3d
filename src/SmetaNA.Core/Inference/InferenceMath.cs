using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Вспомогательная геометрия inference-движка: ближайшие точки, пересечения,
/// принадлежность многоугольнику, касательные к окружности.
/// </summary>
internal static class InferenceMath
{
    private const double ParallelEpsilon = 1e-12;

    public static bool IsFinite(Vector3d v)
        => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);

    public static double Clamp01(double value)
        => value < 0 ? 0 : value > 1 ? 1 : value;

    /// <summary>Ближайшая точка на прямой (point + t·dir) к заданной точке.</summary>
    public static Vector3d ClosestPointOnLine(Vector3d linePoint, Vector3d lineDir, Vector3d query)
    {
        Vector3d dir = lineDir.Normalized();
        double t = Vector3d.Dot(query - linePoint, dir);
        return linePoint + dir * t;
    }

    /// <summary>
    /// Ближайшая точка на прямой (linePoint + t·lineDir) к лучу; <c>false</c> — прямая
    /// параллельна лучу (однозначной ближайшей точки нет).
    /// </summary>
    public static bool TryClosestPointOnLineToRay(Ray3d ray, Vector3d linePoint, Vector3d lineDir, out Vector3d point)
    {
        Vector3d v = lineDir.Normalized();
        Vector3d u = ray.Direction;
        double b = Vector3d.Dot(u, v);
        double denom = 1.0 - b * b;
        point = default;
        if (denom <= ParallelEpsilon)
        {
            return false;
        }

        Vector3d w0 = ray.Origin - linePoint;
        double d = Vector3d.Dot(u, w0);
        double e = Vector3d.Dot(v, w0);
        double s = (b * e - d) / denom;
        double t = e + b * s;
        point = linePoint + v * t;
        return true;
    }

    /// <summary>
    /// Пересечение отрезка с бесконечной прямой: ближайший подход прямых с проверкой
    /// попадания в параметрический диапазон отрезка. Параллельные случаи — <c>false</c>.
    /// </summary>
    public static bool TryIntersectSegmentLine(
        Vector3d aStart, Vector3d aEnd,
        Vector3d linePoint, Vector3d lineDir,
        double tolerance,
        out Vector3d point)
    {
        point = default;
        Vector3d du = (aEnd - aStart).Normalized();
        Vector3d v = lineDir.Normalized();
        double b = Vector3d.Dot(du, v);
        double denom = 1.0 - b * b;
        if (denom <= ParallelEpsilon)
        {
            return false;
        }

        Vector3d w0 = aStart - linePoint;
        double d = Vector3d.Dot(du, w0);
        double e = Vector3d.Dot(v, w0);
        double s = (b * e - d) / denom;
        double t = e + b * s;

        double lengthA = (aEnd - aStart).Length();
        if (s < -tolerance || s > lengthA + tolerance)
        {
            return false;
        }

        Vector3d onA = aStart + du * s;
        Vector3d onLine = linePoint + v * t;
        if ((onA - onLine).Length() > tolerance)
        {
            return false;
        }

        point = (onA + onLine) * 0.5;
        return true;
    }

    /// <summary>
    /// Пересечение отрезков: ближайший подход несмежных прямых с проверкой попадания
    /// в параметрические диапазоны обоих отрезков. Параллельные и совпадающие отрезки
    /// пересечением не считаются.
    /// </summary>
    public static bool TryIntersectSegmentSegment(
        Vector3d aStart, Vector3d aEnd,
        Vector3d bStart, Vector3d bEnd,
        double tolerance,
        out Vector3d point)
    {
        point = default;
        Vector3d du = (aEnd - aStart).Normalized();
        Vector3d dv = (bEnd - bStart).Normalized();
        double b = Vector3d.Dot(du, dv);
        double denom = 1.0 - b * b;
        if (denom <= ParallelEpsilon)
        {
            return false;
        }

        Vector3d w0 = aStart - bStart;
        double d = Vector3d.Dot(du, w0);
        double e = Vector3d.Dot(dv, w0);
        double s = (b * e - d) / denom;
        double t = e + b * s;

        double lengthA = (aEnd - aStart).Length();
        double lengthB = (bEnd - bStart).Length();
        if (s < -tolerance || s > lengthA + tolerance || t < -tolerance || t > lengthB + tolerance)
        {
            return false;
        }

        Vector3d onA = aStart + du * s;
        Vector3d onB = bStart + dv * t;
        if ((onA - onB).Length() > tolerance)
        {
            return false;
        }

        point = (onA + onB) * 0.5;
        return true;
    }

    /// <summary>
    /// Пересечение двух бесконечных прямых: точка середины ближайшего подхода,
    /// если расстояние между прямыми не превышает <paramref name="tolerance"/>.
    /// </summary>
    public static bool TryIntersectLines(
        Vector3d p1, Vector3d d1,
        Vector3d p2, Vector3d d2,
        double tolerance,
        out Vector3d point)
    {
        point = default;
        Vector3d u = d1.Normalized();
        Vector3d v = d2.Normalized();
        double b = Vector3d.Dot(u, v);
        double denom = 1.0 - b * b;
        if (denom <= ParallelEpsilon)
        {
            return false;
        }

        Vector3d w0 = p1 - p2;
        double d = Vector3d.Dot(u, w0);
        double e = Vector3d.Dot(v, w0);
        double s = (b * e - d) / denom;
        double t = e + b * s;

        Vector3d on1 = p1 + u * s;
        Vector3d on2 = p2 + v * t;
        if ((on1 - on2).Length() > tolerance)
        {
            return false;
        }

        point = (on1 + on2) * 0.5;
        return true;
    }

    /// <summary>
    /// Принадлежность точки многоугольнику в плоскости <paramref name="normal"/>
    /// (чёт-нечет по рёбрам кольца); точки на границе считаются принадлежащими.
    /// </summary>
    public static bool PointInRing(IReadOnlyList<Vector3d> ring, Vector3d normal, Vector3d point, double tolerance)
    {
        Vector3d n = normal.Normalized();
        BuildPlaneBasis(n, out Vector3d u, out Vector3d v);
        Vector3d origin = ring[0];

        var polygon = new Vector2d[ring.Count];
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d offset = ring[i] - origin;
            polygon[i] = new Vector2d(Vector3d.Dot(offset, u), Vector3d.Dot(offset, v));
        }

        Vector3d toPoint = point - origin;
        var query = new Vector2d(Vector3d.Dot(toPoint, u), Vector3d.Dot(toPoint, v));

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2d a = polygon[i];
            Vector2d b = polygon[(i + 1) % polygon.Length];
            if (DistanceToSegment2d(query, a, b) <= tolerance)
            {
                return true;
            }
        }

        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2d a = polygon[i];
            Vector2d b = polygon[j];
            if ((a.Y > query.Y) != (b.Y > query.Y))
            {
                double x = (b.X - a.X) * (query.Y - a.Y) / (b.Y - a.Y) + a.X;
                if (query.X < x)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    /// <summary>
    /// Точки касания касательных, проведённых из <paramref name="anchor"/> к окружности.
    /// Якорь должен лежать в плоскости окружности (иначе касательная к кривой не определена)
    /// и вне её; при двух решениях — обе точки.
    /// </summary>
    public static bool TryTangentPoints(
        Vector3d anchor,
        Vector3d center,
        Vector3d normal,
        double radius,
        double planeTolerance,
        out Vector3d first,
        out Vector3d second)
    {
        first = default;
        second = default;

        Vector3d n = normal.Normalized();
        Vector3d a = anchor - center;
        double alongNormal = Vector3d.Dot(a, n);
        if (Math.Abs(alongNormal) > planeTolerance)
        {
            return false;
        }

        Vector3d inPlane = a - n * alongNormal;
        double distance = inPlane.Length();
        if (distance <= radius || distance <= 0)
        {
            return false;
        }

        Vector3d axis = inPlane * (1.0 / distance);
        Vector3d side = Vector3d.Cross(n, axis).Normalized();
        double cos = radius / distance;
        double sin = Math.Sqrt(Math.Max(0, 1.0 - cos * cos));
        Vector3d tangentDirection = axis * cos;
        Vector3d tangentSide = side * sin;

        first = center + (tangentDirection + tangentSide) * radius;
        second = center + (tangentDirection - tangentSide) * radius;
        return true;
    }

    /// <summary>Ортонормированный базис плоскости по единичной нормали.</summary>
    public static void BuildPlaneBasis(Vector3d normal, out Vector3d u, out Vector3d v)
    {
        Vector3d helper = Math.Abs(normal.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitX;
        u = Vector3d.Cross(normal, helper).Normalized();
        v = Vector3d.Cross(normal, u).Normalized();
    }

    private static double DistanceToSegment2d(Vector2d p, Vector2d a, Vector2d b)
    {
        Vector2d ab = b - a;
        double lengthSquared = ab.LengthSquared();
        if (lengthSquared <= 0)
        {
            return Vector2d.Distance(p, a);
        }

        double t = Clamp01(Vector2d.Dot(p - a, ab) / lengthSquared);
        return Vector2d.Distance(p, a + ab * t);
    }
}
