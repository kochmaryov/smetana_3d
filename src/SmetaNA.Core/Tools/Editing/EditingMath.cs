using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Общая математика инструментов редактирования: пересечение луча с плоскостью,
/// проекции в плоскость и знаковые углы (для Rotate/Scale/Move и Offset).
/// </summary>
public static class EditingMath
{
    /// <summary>Порог параллельности луча и плоскости (пересечение неустойчиво).</summary>
    public const double ParallelismThreshold = 1e-9;

    /// <summary>Пересечение луча с плоскостью (точка + нормаль); <c>false</c> — параллельно или позади.</summary>
    public static bool TryIntersectPlane(Ray3d ray, Vector3d planePoint, Vector3d planeNormal, out Vector3d hit)
    {
        hit = default;
        Vector3d normal = planeNormal.Normalized();
        if (normal.LengthSquared() <= 0)
        {
            return false;
        }

        double denominator = Vector3d.Dot(normal, ray.Direction);
        if (Math.Abs(denominator) <= ParallelismThreshold)
        {
            return false;
        }

        double t = Vector3d.Dot(planePoint - ray.Origin, normal) / denominator;
        if (t <= 0)
        {
            return false;
        }

        hit = ray.PointAt(t);
        return true;
    }

    /// <summary>
    /// Точка перетаскивания на «экранно-параллельной» плоскости через
    /// <paramref name="planePoint"/> (нормаль — взгляд камеры): база для Move.
    /// </summary>
    public static bool TryDragPlaneHit(InferenceViewport viewport, Vector2d cursorPx, Vector3d planePoint, out Vector3d hit)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        return TryIntersectPlane(viewport.PickRay(cursorPx), planePoint, viewport.Forward, out hit);
    }

    /// <summary>Проекция вектора в плоскость с заданной нормалью.</summary>
    public static Vector3d ProjectOnPlane(Vector3d vector, Vector3d planeNormal)
    {
        Vector3d normal = planeNormal.Normalized();
        return vector - (normal * Vector3d.Dot(vector, normal));
    }

    /// <summary>
    /// Знаковый угол от <paramref name="from"/> до <paramref name="to"/> вокруг оси
    /// (правая тройка, градусы): положительный — против часовой стрелки со стороны оси.
    /// </summary>
    public static double SignedAngleDegrees(Vector3d from, Vector3d to, Vector3d axis)
    {
        Vector3d u = ProjectOnPlane(from, axis).Normalized();
        Vector3d v = ProjectOnPlane(to, axis).Normalized();
        if (u.LengthSquared() <= 0 || v.LengthSquared() <= 0)
        {
            return 0;
        }

        double cosine = Math.Clamp(Vector3d.Dot(u, v), -1.0, 1.0);
        double sine = Math.Clamp(Vector3d.Dot(Vector3d.Cross(u, v), axis.Normalized()), -1.0, 1.0);
        return Math.Atan2(sine, cosine) * (180.0 / Math.PI);
    }

    /// <summary>Радианы из градусов.</summary>
    public static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);

    /// <summary>Градусы из радианов.</summary>
    public static double ToDegrees(double radians) => radians * (180.0 / Math.PI);

    /// <summary>Мировая трансформация узла: композиция локальных трансформаций предков.</summary>
    public static Transform3d WorldTransformOf(Model.SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Transform3d world = Transform3d.Identity;
        for (Model.SceneNode? current = node; current is not null; current = current.Parent)
        {
            world = Transform3d.Multiply(current.LocalTransform, world);
        }

        return world;
    }
}
