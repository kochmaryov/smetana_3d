namespace SmetaNA.Core.Inference;

/// <summary>
/// Точка/вектор в экранных пикселях: начало координат — левый верхний угол,
/// X вправо, Y вниз (соглашение UI-вьюпорта).
/// </summary>
public readonly record struct Vector2d(double X, double Y)
{
    public static readonly Vector2d Zero = new(0, 0);

    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator *(Vector2d a, double scale) => new(a.X * scale, a.Y * scale);
    public static Vector2d operator *(double scale, Vector2d a) => a * scale;

    public double LengthSquared() => X * X + Y * Y;
    public double Length() => Math.Sqrt(LengthSquared());

    public static double Dot(Vector2d a, Vector2d b) => a.X * b.X + a.Y * b.Y;

    public static double Distance(Vector2d a, Vector2d b) => (a - b).Length();
}
