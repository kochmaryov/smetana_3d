using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats;

/// <summary>Геометрические проверки треугольника (общий путь импорта и валидатора).</summary>
internal static class TriangleGeometry
{
    /// <summary>
    /// Порог вырожденности: квадрат длины векторного произведения &lt; 1e-20 мм⁴,
    /// то есть площадь треугольника меньше 1e-11 мм².
    /// </summary>
    public const double DegenerateCrossLengthSquared = 1e-20;

    /// <summary>Нулевая площадь (вырожденный треугольник).</summary>
    public static bool IsDegenerate(Vector3d a, Vector3d b, Vector3d c) =>
        Vector3d.Cross(b - a, c - a).LengthSquared() < DegenerateCrossLengthSquared;

    /// <summary>Вклад треугольника в ориентированный объём (мм³ при базе в мм).</summary>
    public static double SignedVolumeContribution(Vector3d a, Vector3d b, Vector3d c) =>
        Vector3d.Dot(a, Vector3d.Cross(b, c)) / 6.0;
}
