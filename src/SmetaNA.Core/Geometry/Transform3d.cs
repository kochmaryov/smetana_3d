namespace SmetaNA.Core.Geometry;

/// <summary>
/// Аффинное преобразование 4x4 (double). Соглашение о раскладке — как в System.Numerics:
/// вектор-строка умножается на матрицу, трансляция — в M41..M43.
/// </summary>
public readonly record struct Transform3d(
    double M11, double M12, double M13, double M14,
    double M21, double M22, double M23, double M24,
    double M31, double M32, double M33, double M34,
    double M41, double M42, double M43, double M44)
{
    public static Transform3d Identity => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public static Transform3d CreateTranslation(Vector3d t) => new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        t.X, t.Y, t.Z, 1);

    public static Transform3d Multiply(Transform3d a, Transform3d b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
        a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);

    public static Transform3d operator *(Transform3d a, Transform3d b) => Multiply(a, b);

    public Vector3d TransformPoint(Vector3d p) => new(
        p.X * M11 + p.Y * M21 + p.Z * M31 + M41,
        p.X * M12 + p.Y * M22 + p.Z * M32 + M42,
        p.X * M13 + p.Y * M23 + p.Z * M33 + M43);

    public Vector3d TransformVector(Vector3d v) => new(
        v.X * M11 + v.Y * M21 + v.Z * M31,
        v.X * M12 + v.Y * M22 + v.Z * M32,
        v.X * M13 + v.Y * M23 + v.Z * M33);
}
