using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// Каталог встроенных профилей принтеров (задача 4.5). Габариты и диапазоны
/// слоёв — по паспортам производителей; при обновлении данных правится только
/// этот каталог. Профили можно расширять добавлением элементов в <see cref="All"/>.
/// </summary>
public static class PrinterProfileCatalog
{
    /// <summary>Prusa i3 MK3S+ (250×210×210 мм).</summary>
    public const string PrusaI3Mk3sId = "prusa-i3-mk3s";

    /// <summary>Creality Ender-3 (220×220×250 мм).</summary>
    public const string CrealityEnder3Id = "creality-ender-3";

    /// <summary>Bambu Lab X1 Carbon (256×256×256 мм).</summary>
    public const string BambuX1cId = "bambu-x1c";

    /// <summary>Prusa XL (360×360×360 мм).</summary>
    public const string PrusaXlId = "prusa-xl";

    /// <summary>Creality K1 Max (300×300×300 мм).</summary>
    public const string CrealityK1MaxId = "creality-k1-max";

    private static readonly PrinterProfile[] Profiles =
    [
        new(
            PrusaI3Mk3sId,
            "Prusa Research",
            "i3 MK3S+",
            new Vector3d(250, 210, 210),
            nozzleDiameterMm: 0.4,
            layerHeightMinMm: 0.05,
            layerHeightMaxMm: 0.35,
            notes: "Прямоугольный стол; высота слоя 0,05–0,35 мм (сопло 0,4 мм)."),
        new(
            CrealityEnder3Id,
            "Creality",
            "Ender-3",
            new Vector3d(220, 220, 250),
            nozzleDiameterMm: 0.4,
            layerHeightMinMm: 0.1,
            layerHeightMaxMm: 0.4,
            notes: "Квадратный стол; высота слоя 0,1–0,4 мм (сопло 0,4 мм)."),
        new(
            BambuX1cId,
            "Bambu Lab",
            "X1 Carbon",
            new Vector3d(256, 256, 256),
            nozzleDiameterMm: 0.4,
            layerHeightMinMm: 0.06,
            layerHeightMaxMm: 0.28,
            notes: "Квадратный стол; высота слоя 0,06–0,28 мм (сопло 0,4 мм)."),
        new(
            PrusaXlId,
            "Prusa Research",
            "XL",
            new Vector3d(360, 360, 360),
            nozzleDiameterMm: 0.4,
            layerHeightMinMm: 0.05,
            layerHeightMaxMm: 0.35,
            notes: "Крупная зона печати 360×360×360 мм; высота слоя 0,05–0,35 мм."),
        new(
            CrealityK1MaxId,
            "Creality",
            "K1 Max",
            new Vector3d(300, 300, 300),
            nozzleDiameterMm: 0.4,
            layerHeightMinMm: 0.05,
            layerHeightMaxMm: 0.3,
            notes: "Квадратный стол; высота слоя 0,05–0,3 мм (сопло 0,4 мм)."),
    ];

    /// <summary>Все встроенные профили (в порядке показа в UI).</summary>
    public static IReadOnlyList<PrinterProfile> All => Profiles;

    /// <summary>Профиль по умолчанию (первый в каталоге).</summary>
    public static PrinterProfile Default => Profiles[0];

    /// <summary>Профиль по идентификатору; неизвестный идентификатор — <c>null</c>.</summary>
    public static PrinterProfile? Find(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (PrinterProfile profile in Profiles)
        {
            if (string.Equals(profile.Id, id, StringComparison.Ordinal))
            {
                return profile;
            }
        }

        return null;
    }
}
