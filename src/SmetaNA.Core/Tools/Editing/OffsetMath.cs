using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Математика Offset: концентрический контур в плоскости грани. Положительное
/// расстояние смещает контур внутрь грани (инсет), отрицательное — наружу.
/// Контур строится попарным пересечением смещённых линий рёбер; на вырожденных
/// углах (параллельные линии) применяется смещение по биссектрисе нормалей рёбер.
/// </summary>
public static class OffsetMath
{
    /// <summary>Порог вырожденного пересечения (линии рёбер почти параллельны).</summary>
    public const double DegenerateEpsilon = 1e-9;

    /// <summary>
    /// Контур, смещённый в плоскости (<paramref name="normal"/> — нормаль грани,
    /// обход кольца CCW по правой руке). Число точек совпадает с кольцом,
    /// i-я точка — новый угол между рёбрами (i-1)-м и i-м.
    /// </summary>
    public static IReadOnlyList<Vector3d> OffsetContour(
        IReadOnlyList<Vector3d> ring,
        Vector3d normal,
        double distance)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Count < 3)
        {
            throw new ArgumentException("Контур требует минимум трёх точек.", nameof(ring));
        }

        if (!double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Смещение должно быть конечным.");
        }

        Vector3d n = normal.Normalized();
        if (n.LengthSquared() <= 0)
        {
            throw new ArgumentException("Нормаль плоскости контура вырождена.", nameof(normal));
        }

        if (distance == 0)
        {
            return [.. ring];
        }

        (Vector3d U, Vector3d V) basis = BuildBasis(n);
        Vector3d origin = Centroid(ring);

        int count = ring.Count;
        var points = new (double X, double Y)[count];
        for (int i = 0; i < count; i++)
        {
            Vector3d relative = ring[i] - origin;
            points[i] = (Vector3d.Dot(relative, basis.U), Vector3d.Dot(relative, basis.V));
        }

        // смещённые линии рёбер: внутрь (влево от направления CCW-обхода)
        var lineOrigins = new (double X, double Y)[count];
        var lineDirections = new (double X, double Y)[count];
        for (int i = 0; i < count; i++)
        {
            (double X, double Y) next = points[(i + 1) % count];
            double dx = next.X - points[i].X;
            double dy = next.Y - points[i].Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= DegenerateEpsilon)
            {
                throw new ArgumentException("Контур содержит совпадающие соседние точки.", nameof(ring));
            }

            (double X, double Y) direction = (dx / length, dy / length);
            lineDirections[i] = direction;
            lineOrigins[i] = (points[i].X - (direction.Y * distance), points[i].Y + (direction.X * distance));
        }

        var result = new Vector3d[count];
        for (int i = 0; i < count; i++)
        {
            int previous = (i + count - 1) % count;
            (double X, double Y) corner = IntersectLines(
                lineOrigins[previous], lineDirections[previous],
                lineOrigins[i], lineDirections[i],
                points[i], distance);
            result[i] = origin + (basis.U * corner.X) + (basis.V * corner.Y);
        }

        return result;
    }

    private static (Vector3d U, Vector3d V) BuildBasis(Vector3d normal)
    {
        Vector3d reference = Math.Abs(normal.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitY;
        Vector3d u = Vector3d.Cross(normal, reference).Normalized();
        Vector3d v = Vector3d.Cross(normal, u);
        return (u, v);
    }

    private static Vector3d Centroid(IReadOnlyList<Vector3d> ring)
    {
        Vector3d sum = Vector3d.Zero;
        foreach (Vector3d point in ring)
        {
            sum += point;
        }

        return sum * (1.0 / ring.Count);
    }

    private static (double X, double Y) IntersectLines(
        (double X, double Y) origin1,
        (double X, double Y) direction1,
        (double X, double Y) origin2,
        (double X, double Y) direction2,
        (double X, double Y) corner,
        double distance)
    {
        double cross = (direction1.X * direction2.Y) - (direction1.Y * direction2.X);
        if (Math.Abs(cross) <= DegenerateEpsilon)
        {
            // линии параллельны: угол уходит по биссектрисе нормалей рёбер
            double bx = -direction1.Y - direction2.Y;
            double by = direction1.X + direction2.X;
            double length = Math.Sqrt((bx * bx) + (by * by));
            if (length <= DegenerateEpsilon)
            {
                return (corner.X - (direction1.Y * distance), corner.Y + (direction1.X * distance));
            }

            return (corner.X + ((bx / length) * distance), corner.Y + ((by / length) * distance));
        }

        double t = (((origin2.X - origin1.X) * direction2.Y) - ((origin2.Y - origin1.Y) * direction2.X)) / cross;
        return (origin1.X + (direction1.X * t), origin1.Y + (direction1.Y * t));
    }
}
