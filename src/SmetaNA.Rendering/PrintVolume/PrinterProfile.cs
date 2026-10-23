using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// Профиль 3D-принтера (задача 4.5, M8): габариты зоны печати (build volume),
/// размер сопла и диапазон высот слоя. Все размеры — в миллиметрах.
/// </summary>
public sealed record PrinterProfile
{
    /// <param name="id">Стабильный идентификатор профиля (для настроек/команд).</param>
    /// <param name="manufacturer">Производитель.</param>
    /// <param name="name">Модель принтера.</param>
    /// <param name="buildVolumeMm">
    /// Зона печати: X — ширина (влево/вправо), Y — глубина (вперёд/назад), Z — высота.
    /// </param>
    /// <param name="nozzleDiameterMm">Диаметр штатного сопла, мм.</param>
    /// <param name="layerHeightMinMm">Минимальная высота слоя, мм.</param>
    /// <param name="layerHeightMaxMm">Максимальная высота слоя, мм.</param>
    /// <param name="notes">Примечание (источник данных, особенности).</param>
    public PrinterProfile(
        string id,
        string manufacturer,
        string name,
        Vector3d buildVolumeMm,
        double nozzleDiameterMm,
        double layerHeightMinMm,
        double layerHeightMaxMm,
        string notes = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Идентификатор профиля не может быть пустым.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            throw new ArgumentException("Производитель не может быть пустым.", nameof(manufacturer));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Модель принтера не может быть пустой.", nameof(name));
        }

        if (!IsPositiveFinite(buildVolumeMm.X) || !IsPositiveFinite(buildVolumeMm.Y) || !IsPositiveFinite(buildVolumeMm.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(buildVolumeMm), "Габариты зоны печати должны быть положительными.");
        }

        if (!IsPositiveFinite(nozzleDiameterMm))
        {
            throw new ArgumentOutOfRangeException(nameof(nozzleDiameterMm), "Диаметр сопла должен быть положительным.");
        }

        if (!IsPositiveFinite(layerHeightMinMm) || !IsPositiveFinite(layerHeightMaxMm))
        {
            throw new ArgumentOutOfRangeException(nameof(layerHeightMinMm), "Высоты слоя должны быть положительными.");
        }

        if (layerHeightMinMm > layerHeightMaxMm)
        {
            throw new ArgumentException("Минимальная высота слоя больше максимальной.", nameof(layerHeightMinMm));
        }

        Id = id;
        Manufacturer = manufacturer;
        Name = name;
        BuildVolumeMm = buildVolumeMm;
        NozzleDiameterMm = nozzleDiameterMm;
        LayerHeightMinMm = layerHeightMinMm;
        LayerHeightMaxMm = layerHeightMaxMm;
        Notes = notes ?? string.Empty;
    }

    /// <summary>Стабильный идентификатор профиля (для настроек/команд).</summary>
    public string Id { get; }

    /// <summary>Производитель.</summary>
    public string Manufacturer { get; }

    /// <summary>Модель принтера.</summary>
    public string Name { get; }

    /// <summary>Зона печати: X — ширина, Y — глубина, Z — высота, мм.</summary>
    public Vector3d BuildVolumeMm { get; }

    /// <summary>Диаметр штатного сопла, мм.</summary>
    public double NozzleDiameterMm { get; }

    /// <summary>Минимальная высота слоя, мм.</summary>
    public double LayerHeightMinMm { get; }

    /// <summary>Максимальная высота слоя, мм.</summary>
    public double LayerHeightMaxMm { get; }

    /// <summary>Примечание (источник данных, особенности).</summary>
    public string Notes { get; }

    /// <summary>Полное имя профиля «Производитель Модель».</summary>
    public string DisplayName => $"{Manufacturer} {Name}";

    /// <summary>
    /// Зона печати в мировых координатах: центр стола в начале координат XY,
    /// стол в плоскости Z=0, высота — вдоль +Z (согласовано с сеткой вьюпорта).
    /// </summary>
    public BoundingBox3d BuildVolumeBounds
    {
        get
        {
            double halfX = BuildVolumeMm.X * 0.5;
            double halfY = BuildVolumeMm.Y * 0.5;
            return new BoundingBox3d(
                new Vector3d(-halfX, -halfY, 0),
                new Vector3d(halfX, halfY, BuildVolumeMm.Z));
        }
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}
