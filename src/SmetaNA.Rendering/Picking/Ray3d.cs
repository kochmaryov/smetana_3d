using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>Луч в мировых координатах (мм): начало + единичное направление.</summary>
public readonly record struct Ray3d(Vector3d Origin, Vector3d Direction)
{
    public Vector3d GetPoint(double t) => Origin + Direction * t;
}
