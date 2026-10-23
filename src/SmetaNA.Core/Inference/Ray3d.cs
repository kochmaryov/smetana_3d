using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Луч в мировых координатах: точка отсчёта и единичное направление
/// (нормализуется в конструкторе). Параметр вдоль луча измеряется в мировых единицах.
/// </summary>
public readonly record struct Ray3d
{
    public Vector3d Origin { get; }
    public Vector3d Direction { get; }

    public Ray3d(Vector3d origin, Vector3d direction)
    {
        if (!InferenceMath.IsFinite(origin))
        {
            throw new ArgumentException("Начало луча должно быть конечным.", nameof(origin));
        }

        if (!InferenceMath.IsFinite(direction))
        {
            throw new ArgumentException("Направление луча должно быть конечным.", nameof(direction));
        }

        double length = direction.Length();
        if (length <= 0)
        {
            throw new ArgumentException("Направление луча не может быть нулевым.", nameof(direction));
        }

        Origin = origin;
        Direction = direction * (1.0 / length);
    }

    public Vector3d PointAt(double distance) => Origin + Direction * distance;

    /// <summary>Параметр вдоль луча ближайшей точки к <paramref name="point"/> (может быть отрицательным).</summary>
    public double ClosestParameterTo(Vector3d point) => Vector3d.Dot(point - Origin, Direction);

    /// <summary>Квадрат расстояния от точки до полупрямой.</summary>
    public double DistanceSquaredTo(Vector3d point)
    {
        double t = ClosestParameterTo(point);
        Vector3d closest = t <= 0 ? Origin : PointAt(t);
        return (point - closest).LengthSquared();
    }
}
