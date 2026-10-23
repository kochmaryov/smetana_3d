using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Geometry;

/// <summary>
/// Плоскость построения инструментов рисования: точка, нормаль и ортонормированный
/// базис (U, V) — в нём строятся прямоугольники, окружности и многоугольники.
/// Базис правый: U × V = Normal.
/// </summary>
/// <param name="Origin">Точка плоскости.</param>
/// <param name="Normal">Единичная нормаль плоскости.</param>
/// <param name="U">Единичный базисный вектор.</param>
/// <param name="V">Единичный базисный вектор (Normal × U).</param>
public readonly record struct DrawingPlane(Vector3d Origin, Vector3d Normal, Vector3d U, Vector3d V)
{
    /// <summary>Плоскость XY («земля») — построение без грани под курсором.</summary>
    public static DrawingPlane Ground => Create(Vector3d.Zero, Vector3d.UnitZ);

    /// <summary>
    /// Плоскость через точку с нормалью; <paramref name="uHint"/> (если задан)
    /// задаёт направление оси U, иначе берётся мировая ось, наименее параллельная нормали.
    /// </summary>
    public static DrawingPlane Create(Vector3d origin, Vector3d normal, Vector3d? uHint = null)
    {
        Vector3d n = normal.Normalized();
        if (n.LengthSquared() <= 0)
        {
            throw new ArgumentException("Нормаль плоскости не может быть нулевой.", nameof(normal));
        }

        Vector3d u = default;
        if (uHint is { } hint)
        {
            Vector3d rejected = hint - n * Vector3d.Dot(hint, n);
            if (rejected.LengthSquared() > 0)
            {
                u = rejected.Normalized();
            }
        }

        if (u.LengthSquared() <= 0)
        {
            Vector3d axis = Math.Abs(n.X) <= Math.Abs(n.Y) && Math.Abs(n.X) <= Math.Abs(n.Z)
                ? Vector3d.UnitX
                : Math.Abs(n.Y) <= Math.Abs(n.Z)
                    ? Vector3d.UnitY
                    : Vector3d.UnitZ;
            u = (axis - n * Vector3d.Dot(axis, n)).Normalized();
        }

        Vector3d v = Vector3d.Cross(n, u).Normalized();
        return new DrawingPlane(origin, n, u, v);
    }

    /// <summary>Точка плоскости по координатам в базисе (U, V).</summary>
    public Vector3d PointAt(double u, double v) => Origin + U * u + V * v;

    /// <summary>Проекция точки на плоскость.</summary>
    public Vector3d Project(Vector3d point) => point - Normal * Vector3d.Dot(point - Origin, Normal);

    /// <summary>Координаты проекции точки в базисе (U, V).</summary>
    public (double U, double V) Coordinates(Vector3d point)
    {
        Vector3d delta = Project(point) - Origin;
        return (Vector3d.Dot(delta, U), Vector3d.Dot(delta, V));
    }

    /// <summary>Пересечение плоскости с лучом; <c>false</c> — луч параллелен плоскости.</summary>
    public bool TryIntersect(Ray3d ray, out Vector3d point)
    {
        double denominator = Vector3d.Dot(ray.Direction, Normal);
        if (Math.Abs(denominator) <= 1e-12)
        {
            point = default;
            return false;
        }

        double distance = Vector3d.Dot(Origin - ray.Origin, Normal) / denominator;
        point = ray.PointAt(distance);
        return true;
    }
}

/// <summary>Обратные аффинные преобразования (в <see cref="Transform3d"/> их нет).</summary>
public static class TransformInverter
{
    /// <summary>
    /// Обратное аффинное преобразование (линейная часть 3×3 + трансляция).
    /// Вырожденная линейная часть — <c>false</c>.
    /// </summary>
    public static bool TryInvert(Transform3d transform, out Transform3d inverse)
    {
        double a = transform.M11, b = transform.M12, c = transform.M13;
        double d = transform.M21, e = transform.M22, f = transform.M23;
        double g = transform.M31, h = transform.M32, i = transform.M33;

        double cofA = (e * i) - (f * h);
        double cofB = (f * g) - (d * i);
        double cofC = (d * h) - (e * g);
        double determinant = (a * cofA) + (b * cofB) + (c * cofC);
        if (Math.Abs(determinant) <= 1e-15)
        {
            inverse = default;
            return false;
        }

        double inv = 1.0 / determinant;
        // транспонированная матрица алгебраических дополнений × 1/det
        double m11 = cofA * inv;
        double m21 = cofB * inv;
        double m31 = cofC * inv;
        double m12 = ((c * h) - (b * i)) * inv;
        double m22 = ((a * i) - (c * g)) * inv;
        double m32 = ((b * g) - (a * h)) * inv;
        double m13 = ((b * f) - (c * e)) * inv;
        double m23 = ((c * d) - (a * f)) * inv;
        double m33 = ((a * e) - (b * d)) * inv;

        var translation = new Vector3d(transform.M41, transform.M42, transform.M43);
        Vector3d moved = new(
            -(translation.X * m11 + translation.Y * m21 + translation.Z * m31),
            -(translation.X * m12 + translation.Y * m22 + translation.Z * m32),
            -(translation.X * m13 + translation.Y * m23 + translation.Z * m33));

        inverse = new Transform3d(
            m11, m12, m13, 0,
            m21, m22, m23, 0,
            m31, m32, m33, 0,
            moved.X, moved.Y, moved.Z, 1);
        return true;
    }
}
