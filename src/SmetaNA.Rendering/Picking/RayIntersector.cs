using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Пересечения луча с треугольником (алгоритм Мёллера–Трамбора) и с AABB (slab-тест).
/// Всё в double: погрешность пика должна укладываться в допуск DoD §5.4 (0,01 мм).
/// </summary>
public static class RayIntersector
{
    private const double Epsilon = 1e-12;

    /// <summary>
    /// Пересечение луча с треугольником a-b-c (двойная сторона).
    /// <paramref name="t"/> — дистанция вдоль луча, u/v — барицентрические координаты.
    /// </summary>
    public static bool TryIntersectTriangle(
        Ray3d ray,
        Vector3d a,
        Vector3d b,
        Vector3d c,
        out double t,
        out double u,
        out double v)
    {
        t = 0;
        u = 0;
        v = 0;

        Vector3d edge1 = b - a;
        Vector3d edge2 = c - a;
        Vector3d p = Vector3d.Cross(ray.Direction, edge2);
        double det = Vector3d.Dot(edge1, p);
        if (det > -Epsilon && det < Epsilon)
        {
            return false;
        }

        double invDet = 1.0 / det;
        Vector3d s = ray.Origin - a;
        u = Vector3d.Dot(s, p) * invDet;
        if (u < 0 || u > 1)
        {
            return false;
        }

        Vector3d q = Vector3d.Cross(s, edge1);
        v = Vector3d.Dot(ray.Direction, q) * invDet;
        if (v < 0 || u + v > 1)
        {
            return false;
        }

        t = Vector3d.Dot(edge2, q) * invDet;
        return t >= 0;
    }

    /// <summary>
    /// Пересечение луча с ограничивающим ящиком. <paramref name="tEnter"/> — ближняя
    /// точка входа (0, если начало луча внутри ящика); промах — false.
    /// </summary>
    public static bool TryIntersectBox(Ray3d ray, BoundingBox3d box, out double tEnter)
    {
        tEnter = 0;
        if (box.IsEmpty)
        {
            return false;
        }

        double tMin = double.NegativeInfinity;
        double tMax = double.PositiveInfinity;

        if (!Slab(ray.Origin.X, ray.Direction.X, box.Min.X, box.Max.X, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Y, ray.Direction.Y, box.Min.Y, box.Max.Y, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Z, ray.Direction.Z, box.Min.Z, box.Max.Z, ref tMin, ref tMax))
        {
            return false;
        }

        if (tMax < 0)
        {
            return false;
        }

        tEnter = Math.Max(tMin, 0);
        return true;
    }

    private static bool Slab(double origin, double direction, double min, double max, ref double tMin, ref double tMax)
    {
        if (Math.Abs(direction) < Epsilon)
        {
            return origin >= min && origin <= max;
        }

        double inv = 1.0 / direction;
        double t1 = (min - origin) * inv;
        double t2 = (max - origin) * inv;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }
}
