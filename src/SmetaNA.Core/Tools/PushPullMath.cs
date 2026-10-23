using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools;

/// <summary>
/// Математика push/pull: расстояние вдоль оси выдавливания по лучу курсора.
/// </summary>
public static class PushPullMath
{
    /// <summary>
    /// Порог «луч почти параллелен оси» (|cos| ≳ 0,95): метод ближайших точек
    /// двух прямых в этой области неустойчив, применяется запасной расчёт.
    /// </summary>
    public const double ParallelismThreshold = 0.95;

    /// <summary>
    /// Смещение вдоль оси (<paramref name="anchor"/>, <paramref name="axis"/>) для
    /// луча курсора: расстояние от якоря до точки оси, ближайшей к лучу
    /// (классическое поведение push/pull в 3D-видах). Если луч почти параллелен
    /// оси (вид «в лоб»), используется проекция ближайшей точки луча к якорю —
    /// устойчивое, но слабо чувствительное значение; точное расстояние в этом
    /// случае задаётся числовым вводом (VCB).
    /// </summary>
    public static double OffsetAlongAxis(Ray3d cursorRay, Vector3d anchor, Vector3d axis)
    {
        Vector3d u = axis.Normalized();
        if (u.LengthSquared() <= 0)
        {
            throw new ArgumentException("Ось выдавливания вырождена.", nameof(axis));
        }

        Vector3d v = cursorRay.Direction;
        Vector3d w0 = anchor - cursorRay.Origin;
        double b = Vector3d.Dot(u, v);
        double d = Vector3d.Dot(u, w0);
        double e = Vector3d.Dot(v, w0);

        if (Math.Abs(b) >= ParallelismThreshold)
        {
            Vector3d closestToAnchor = cursorRay.PointAt(e);
            return Vector3d.Dot(closestToAnchor - anchor, u);
        }

        return ((b * e) - d) / (1.0 - (b * b));
    }
}
