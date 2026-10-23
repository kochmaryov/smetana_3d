using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Эталонные модели (golden-меши, задачи 4.2–4.4 / DoD §5.3): простые фиксированные
/// меши в миллиметрах, все замкнуты и ориентированы нормалями наружу.
/// </summary>
internal static class GoldenMeshes
{
    /// <summary>Куб 100×100×100 мм (DoD §5.4: размер 100,00 мм).</summary>
    public static MeshData Cube() => new(CubePositions, CubeIndices);

    /// <summary>«Стакан» 60×60×80 мм: стенка 5 мм, дно 5 мм, открытая верхняя грань.</summary>
    public static MeshData Cup() => new(CupPositions, CupIndices);

    /// <summary>«Подставка»: ступенька 100×60×50 мм (база 100×60×20 + верх 60×60×30).</summary>
    public static MeshData Stand() => new(StandPositions, StandIndices);

    /// <summary>Ориентированный объём куба, мм³.</summary>
    public const double CubeVolumeMm3 = 1_000_000;

    /// <summary>Ориентированный объём стакана, мм³: 60·60·80 − 50·50·75.</summary>
    public const double CupVolumeMm3 = 100_500;

    /// <summary>Ориентированный объём подставки, мм³: 100·60·20 + 60·60·30.</summary>
    public const double StandVolumeMm3 = 228_000;

    private static readonly Vector3d[] CubePositions =
    [
        new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), new(0, 100, 0),
        new(0, 0, 100), new(100, 0, 100), new(100, 100, 100), new(0, 100, 100),
    ];

    private static readonly int[] CubeIndices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
    ];

    private static readonly Vector3d[] CupPositions =
    [
        new(0, 0, 0), new(60, 0, 0), new(60, 60, 0), new(0, 60, 0),
        new(0, 0, 80), new(60, 0, 80), new(60, 60, 80), new(0, 60, 80),
        new(5, 5, 80), new(55, 5, 80), new(55, 55, 80), new(5, 55, 80),
        new(5, 5, 5), new(55, 5, 5), new(55, 55, 5), new(5, 55, 5),
    ];

    private static readonly int[] CupIndices =
    [
        0, 3, 2, 0, 2, 1,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
        4, 5, 9, 4, 9, 8,
        5, 6, 10, 5, 10, 9,
        6, 7, 11, 6, 11, 10,
        7, 4, 8, 7, 8, 11,
        12, 8, 9, 12, 9, 13,
        13, 9, 10, 13, 10, 14,
        15, 14, 10, 15, 10, 11,
        12, 15, 11, 12, 11, 8,
        12, 13, 14, 12, 14, 15,
    ];

    private static readonly Vector3d[] StandPositions =
    [
        new(0, 0, 0), new(100, 0, 0), new(100, 60, 0), new(0, 60, 0),
        new(0, 0, 20), new(20, 0, 20), new(80, 0, 20), new(100, 0, 20),
        new(100, 60, 20), new(80, 60, 20), new(20, 60, 20), new(0, 60, 20),
        new(20, 0, 50), new(80, 0, 50), new(80, 60, 50), new(20, 60, 50),
    ];

    private static readonly int[] StandIndices =
    [
        0, 3, 2, 0, 2, 1,
        12, 13, 14, 12, 14, 15,
        4, 5, 10, 4, 10, 11,
        6, 7, 8, 6, 8, 9,
        0, 1, 7, 0, 7, 6, 0, 6, 5, 0, 5, 4,
        5, 6, 13, 5, 13, 12,
        2, 3, 11, 2, 11, 10, 2, 10, 9, 2, 9, 8,
        9, 10, 15, 9, 15, 14,
        3, 0, 4, 3, 4, 11,
        1, 2, 8, 1, 8, 7,
        10, 5, 12, 10, 12, 15,
        6, 9, 14, 6, 14, 13,
    ];
}
