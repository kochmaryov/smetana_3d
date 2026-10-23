using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Трансформации для инструментов Move/Rotate/Scale: обращение аффинных матриц,
/// построение вращения/масштаба вокруг точки и перенос мирового преобразования
/// в локальное пространство узла (L' = L ∘ (W⁻¹ ∘ D ∘ W)).
/// </summary>
public static class EditingTransforms
{
    /// <summary>Порог вырожденной матрицы (нулевой определитель).</summary>
    public const double DegenerateEpsilon = 1e-12;

    /// <summary>Обратная аффинная матрица; <c>false</c> — матрица вырождена.</summary>
    public static bool TryInverse(Transform3d transform, out Transform3d inverse)
    {
        inverse = default;

        double a = transform.M11, b = transform.M12, c = transform.M13;
        double d = transform.M21, e = transform.M22, f = transform.M23;
        double g = transform.M31, h = transform.M32, i = transform.M33;

        double determinant = (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));
        if (Math.Abs(determinant) <= DegenerateEpsilon)
        {
            return false;
        }

        double inv = 1.0 / determinant;
        inverse = new Transform3d(
            ((e * i) - (f * h)) * inv, ((c * h) - (b * i)) * inv, ((b * f) - (c * e)) * inv, 0,
            ((f * g) - (d * i)) * inv, ((a * i) - (c * g)) * inv, ((c * d) - (a * f)) * inv, 0,
            ((d * h) - (e * g)) * inv, ((b * g) - (a * h)) * inv, ((a * e) - (b * d)) * inv, 0,
            0, 0, 0, 1);

        // перевод — последняя строка: t' = -t × L⁻¹
        double tx = transform.M41, ty = transform.M42, tz = transform.M43;
        inverse = new Transform3d(
            inverse.M11, inverse.M12, inverse.M13, inverse.M14,
            inverse.M21, inverse.M22, inverse.M23, inverse.M24,
            inverse.M31, inverse.M32, inverse.M33, inverse.M34,
            -((tx * inverse.M11) + (ty * inverse.M21) + (tz * inverse.M31)),
            -((tx * inverse.M12) + (ty * inverse.M22) + (tz * inverse.M32)),
            -((tx * inverse.M13) + (ty * inverse.M23) + (tz * inverse.M33)),
            1);
        return true;
    }

    /// <summary>Вращение вокруг оси через точку (правая тройка, радианы).</summary>
    public static Transform3d CreateRotation(Vector3d axis, Vector3d pivot, double radians)
    {
        if (!double.IsFinite(radians))
        {
            throw new ArgumentOutOfRangeException(nameof(radians), "Угол должен быть конечным.");
        }

        Vector3d k = axis.Normalized();
        if (k.LengthSquared() <= 0)
        {
            throw new ArgumentException("Ось вращения вырождена.", nameof(axis));
        }

        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);

        Vector3d Map(Vector3d v) =>
            (v * cosine) + (Vector3d.Cross(k, v) * sine) + (k * (Vector3d.Dot(k, v) * (1.0 - cosine)));

        // строки матрицы — образы базисных векторов (вектор-строка × матрица)
        Vector3d row1 = Map(Vector3d.UnitX);
        Vector3d row2 = Map(Vector3d.UnitY);
        Vector3d row3 = Map(Vector3d.UnitZ);
        Vector3d moved = Map(pivot);

        return new Transform3d(
            row1.X, row1.Y, row1.Z, 0,
            row2.X, row2.Y, row2.Z, 0,
            row3.X, row3.Y, row3.Z, 0,
            pivot.X - moved.X, pivot.Y - moved.Y, pivot.Z - moved.Z, 1);
    }

    /// <summary>Равномерное масштабирование вокруг точки.</summary>
    public static Transform3d CreateUniformScale(Vector3d pivot, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Масштаб должен быть конечным и положительным.");
        }

        return new Transform3d(
            factor, 0, 0, 0,
            0, factor, 0, 0,
            0, 0, factor, 0,
            pivot.X * (1.0 - factor), pivot.Y * (1.0 - factor), pivot.Z * (1.0 - factor), 1);
    }

    /// <summary>
    /// Новая локальная трансформация узла при мировом преобразовании <paramref name="worldDelta"/>:
    /// L' = L ∘ (W⁻¹ ∘ D ∘ W), где W — текущая мировая трансформация узла.
    /// </summary>
    public static bool TryComposeWorldDelta(
        Transform3d local,
        Transform3d world,
        Transform3d worldDelta,
        out Transform3d newLocal)
    {
        newLocal = default;
        if (!TryInverse(world, out Transform3d inverseWorld))
        {
            return false;
        }

        Transform3d conjugated = Transform3d.Multiply(
            Transform3d.Multiply(inverseWorld, worldDelta),
            world);
        newLocal = Transform3d.Multiply(local, conjugated);
        return true;
    }

    /// <summary>
    /// Локальный образ мировой трансформации: точки меша переводятся в мир,
    /// преобразуются <paramref name="worldDelta"/> и возвращаются обратно.
    /// </summary>
    public static bool TryConjugatedMap(
        Transform3d world,
        Transform3d worldDelta,
        out Func<Vector3d, Vector3d> map)
    {
        map = static _ => default;
        if (!TryInverse(world, out Transform3d inverseWorld))
        {
            return false;
        }

        map = point => inverseWorld.TransformPoint(worldDelta.TransformPoint(world.TransformPoint(point)));
        return true;
    }
}
