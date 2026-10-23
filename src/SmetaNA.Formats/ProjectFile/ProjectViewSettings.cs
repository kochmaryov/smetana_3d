using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>Режим проекции камеры проекта (модуль M9).</summary>
public enum ProjectProjectionMode
{
    /// <summary>Перспектива: масштаб — через расстояние до цели.</summary>
    Perspective,

    /// <summary>Ортография: масштаб — через высоту видимой области.</summary>
    Orthographic,
}

/// <summary>
/// Настройки вида проекта: орбитальная камера и режим проекции (модуль M9, «настройки вида»).
/// Поля и инварианты совпадают со SmetaNA.Rendering.Camera 1:1 (маппинг механический),
/// чтобы редактор мог переносить вид без потери точности. Числа хранятся как double
/// и при сериализации сохраняются бит-в-бит.
/// </summary>
public sealed record ProjectViewSettings
{
    /// <summary>Режим проекции.</summary>
    public ProjectProjectionMode Projection { get; init; } = ProjectProjectionMode.Perspective;

    /// <summary>Точка, вокруг которой вращается камера (мм).</summary>
    public Vector3d Target { get; init; } = Vector3d.Zero;

    /// <summary>Расстояние от камеры до цели (мм).</summary>
    public double Distance { get; init; } = 1000;

    /// <summary>Поворот орбиты вокруг Z (радианы).</summary>
    public double Yaw { get; init; } = Math.PI / 4;

    /// <summary>Наклон орбиты (радианы, ±π/2).</summary>
    public double Pitch { get; init; } = 0.6154797086703874; // atan(1/√2) — истинная изометрия

    /// <summary>Вертикальный угол зрения перспективы (радианы).</summary>
    public double FieldOfViewY { get; init; } = Math.PI / 4;

    /// <summary>Высота видимой области ортографии (мм).</summary>
    public double OrthoHeight { get; init; } = 1000;

    /// <summary>Отношение ширины вьюпорта к высоте.</summary>
    public double AspectRatio { get; init; } = 1;

    /// <summary>Ближняя плоскость отсечения (мм).</summary>
    public double NearPlane { get; init; } = 1;

    /// <summary>Дальняя плоскость отсечения (мм).</summary>
    public double FarPlane { get; init; } = 100000;

    /// <summary>Вид по умолчанию: перспективная изометрия (как у камеры редактора).</summary>
    public static ProjectViewSettings Default => new();
}
