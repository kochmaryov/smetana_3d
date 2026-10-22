namespace SmetaNA.Core.Geometry;

/// <summary>Вектор/точка двойной точности: допуск 0,01 мм по DoD требует double.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static readonly Vector3d Zero = new(0, 0, 0);
    public static readonly Vector3d UnitX = new(1, 0, 0);
    public static readonly Vector3d UnitY = new(0, 1, 0);
    public static readonly Vector3d UnitZ = new(0, 0, 1);

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3d operator *(Vector3d a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
    public static Vector3d operator *(double scale, Vector3d a) => a * scale;

    public double LengthSquared() => X * X + Y * Y + Z * Z;
    public double Length() => Math.Sqrt(LengthSquared());

    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3d Cross(Vector3d a, Vector3d b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    public Vector3d Normalized()
    {
        double length = Length();
        return length <= 0 ? Zero : this * (1.0 / length);
    }
}
