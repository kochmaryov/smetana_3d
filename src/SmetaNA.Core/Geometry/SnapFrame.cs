using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Geometry;

/// <summary>
/// Ориентированная точка сборки (пользовательский Snap): позиция, нормаль диска
/// (<see cref="Direction"/>) и поворот вокруг нормали (<see cref="Up"/>).
/// Модель повторяет <c>Sketchup::Snap</c> Ruby API: позиция, direction и up —
/// единичные векторы; угол вокруг direction не хранится.
/// </summary>
public readonly record struct SnapFrame(Vector3d Position, Vector3d Direction, Vector3d Up)
{
    /// <summary>Кадр по умолчанию в начале координат: нормаль по синей оси, up по зелёной.</summary>
    public static SnapFrame World => new(Vector3d.Zero, Vector3d.UnitZ, Vector3d.UnitY);

    /// <summary>
    /// Создать кадр: нормаль и up нормализуются, up ортогонализуется нормали.
    /// <c>false</c> — нулевая нормаль или вырожденный up (нельзя построить поворот).
    /// </summary>
    public static bool TryCreate(Vector3d position, Vector3d direction, Vector3d up, out SnapFrame frame)
    {
        frame = default;
        if (!InferenceMath.IsFinite(position)
            || !InferenceMath.IsFinite(direction)
            || !InferenceMath.IsFinite(up)
            || direction.LengthSquared() <= 1e-18)
        {
            return false;
        }

        Vector3d normal = direction.Normalized();
        Vector3d orthogonalUp = up - normal * Vector3d.Dot(up, normal);
        if (orthogonalUp.LengthSquared() <= 1e-18)
        {
            // up совпал с нормалью — берём ось, наименее параллельную нормали.
            Vector3d axis = Math.Abs(normal.X) <= Math.Abs(normal.Y) && Math.Abs(normal.X) <= Math.Abs(normal.Z)
                ? Vector3d.UnitX
                : Math.Abs(normal.Y) <= Math.Abs(normal.Z)
                    ? Vector3d.UnitY
                    : Vector3d.UnitZ;
            orthogonalUp = axis - normal * Vector3d.Dot(axis, normal);
        }

        frame = new SnapFrame(position, normal, orthogonalUp.Normalized());
        return true;
    }

    /// <summary>Кадр с нормализованными векторами (без исключений: вырожденный — кадр мира).</summary>
    public SnapFrame Normalized()
        => TryCreate(Position, Direction, Up, out SnapFrame frame) ? frame : World with { Position = Position };

    /// <summary>Третья ось кадра (правый базис: Up × Direction).</summary>
    public Vector3d Right => Vector3d.Cross(Up, Direction).Normalized();

    /// <summary>
    /// Соединение «лицевая к лицевой» (snap-to-snap): нормаль подвижного кадра
    /// становится противоположной нормали целевого, up совпадает; позиция — целевая точка.
    /// </summary>
    public static SnapFrame FrontToFront(SnapFrame moving, SnapFrame target)
    {
        Vector3d targetNormal = target.Normalized().Direction;
        return TryCreate(target.Position, -targetNormal, target.Up, out SnapFrame frame)
            ? frame
            : moving with { Position = target.Position };
    }

    /// <summary>Кадр в мировых координатах: позиция и оси трансформируются, масштаб снимается.</summary>
    public SnapFrame Transformed(Transform3d transform)
        => TryCreate(
            transform.TransformPoint(Position),
            transform.TransformVector(Direction),
            transform.TransformVector(Up),
            out SnapFrame frame)
            ? frame
            : this;

    /// <summary>Матрица ориентации кадра (оси — столбцы), позиция — в трансляции.</summary>
    public Transform3d ToTransform() => new(
        Right.X, Right.Y, Right.Z, 0,
        Up.X, Up.Y, Up.Z, 0,
        Direction.X, Direction.Y, Direction.Z, 0,
        Position.X, Position.Y, Position.Z, 1);

    /// <summary>
    /// Преобразование, совмещающее этот кадр с целевым: позиция переходит в позицию
    /// цели, оси — в оси цели (поворот + перенос). Используется для доворота
    /// объекта при snap-to-snap.
    /// </summary>
    public Transform3d DeltaTo(SnapFrame target)
    {
        SnapFrame from = Normalized();
        SnapFrame to = target.Normalized();

        Transform3d rotation = Transform3d.Multiply(
            to.Basis(),
            TransposeBasis(from.Basis()));
        Vector3d translation = to.Position - rotation.TransformVector(from.Position);
        return Transform3d.Multiply(rotation, Transform3d.CreateTranslation(translation));
    }

    /// <summary>Ориентация кадра без переноса (для композиции поворотов).</summary>
    private Transform3d Basis() => new(
        Right.X, Right.Y, Right.Z, 0,
        Up.X, Up.Y, Up.Z, 0,
        Direction.X, Direction.Y, Direction.Z, 0,
        0, 0, 0, 1);

    /// <summary>Транспонированная линейная часть (обратный поворот ортонормального базиса).</summary>
    private static Transform3d TransposeBasis(Transform3d matrix) => new(
        matrix.M11, matrix.M21, matrix.M31, 0,
        matrix.M12, matrix.M22, matrix.M32, 0,
        matrix.M13, matrix.M23, matrix.M33, 0,
        0, 0, 0, 1);
}
