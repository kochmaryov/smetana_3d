using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>Вид сущности снимка, к которой относится кандидат привязки.</summary>
public enum SnapEntityKind
{
    None,
    Vertex,
    Edge,
    Face,
    Circle,
    Guide,
    Node,
    ComponentInstance,
    Grid,
    Origin,
    UserSnap,
}

/// <summary>
/// Ссылка на источник привязки: узел сцены и элемент снимка. Достаточна, чтобы UI
/// подсветил ребро/грань без повторного поиска и показал имя контейнера.
/// Не переживает правку сцены: снимок инвалидируется вместе с ней.
/// </summary>
/// <param name="NodeId">Узел сцены (меш, группа или инстанс компонента).</param>
/// <param name="Kind">Вид сущности.</param>
/// <param name="ElementIndex">Индекс ребра/грани/окружности в снимке — для подсветки.</param>
/// <param name="DisplayName">Имя группы/компонента для подписи.</param>
public readonly record struct SnapEntityRef(
    Guid? NodeId,
    SnapEntityKind Kind,
    int? ElementIndex,
    string? DisplayName)
{
    /// <summary>Источник не определён (производные привязки: пересечения, сетка).</summary>
    public static readonly SnapEntityRef None = default;

    public bool IsEmpty => Kind == SnapEntityKind.None;
}

/// <summary>Линия ограничения для отрисовки: ось, параллель, продолжение ребра.</summary>
public readonly record struct SnapLine(Vector3d Origin, Vector3d Direction)
{
    public SnapLine Normalized() => new(Origin, Direction.Normalized());
}

/// <summary>
/// Плоскость сетки привязки: начало, два ортогональных направления и шаг в мировых
/// единицах. Сетка — наше расширение (в SketchUp Desktop её нет).
/// </summary>
public sealed record SnapGrid
{
    public SnapGrid(Vector3d origin, Vector3d axisU, Vector3d axisV, double step)
    {
        if (!InferenceMath.IsFinite(origin))
        {
            throw new ArgumentException("Начало сетки должно быть конечным.", nameof(origin));
        }

        if (!InferenceMath.IsFinite(axisU) || axisU.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление U сетки должно быть ненулевым.", nameof(axisU));
        }

        if (!InferenceMath.IsFinite(axisV) || axisV.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление V сетки должно быть ненулевым.", nameof(axisV));
        }

        if (!double.IsFinite(step) || step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Шаг сетки должен быть положительным.");
        }

        Vector3d u = axisU.Normalized();
        Vector3d v = axisV.Normalized();
        if (Vector3d.Cross(u, v).LengthSquared() <= 1e-12)
        {
            throw new ArgumentException("Направления сетки не должны быть коллинеарны.", nameof(axisV));
        }

        Origin = origin;
        AxisU = u;
        AxisV = v;
        Step = step;
    }

    public Vector3d Origin { get; }
    public Vector3d AxisU { get; }
    public Vector3d AxisV { get; }
    public double Step { get; }

    /// <summary>Нормаль плоскости сетки (U × V).</summary>
    public Vector3d Normal => Vector3d.Cross(AxisU, AxisV).Normalized();

    /// <summary>Радиус прилипания к узлу сетки, пикселей.</summary>
    public double TolerancePx { get; init; } = 12.0;

    public bool IsEnabled { get; init; } = true;

    /// <summary>Сетка на плоскости XY с началом в мировом нуле.</summary>
    public static SnapGrid Ground(double step) => new(Vector3d.Zero, Vector3d.UnitX, Vector3d.UnitY, step);
}
