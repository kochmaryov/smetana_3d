namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// Готовый оверлей зоны печати для кадра вьюпорта (задача 4.5): каркас зоны,
/// сетка пола, треугольники «за пределами зоны» и попадание модели в зону.
/// Строится из профиля принтера и модели сцены; при <c>null</c> модели
/// геометрия сцены не учитывается (только каркас и сетка).
/// </summary>
public sealed class PrintVolumeOverlay
{
    private PrintVolumeOverlay(
        PrinterProfile profile,
        float[] frameLines,
        float[] floorGridLines,
        double floorGridStepMm,
        float[] outOfVolumeTriangles,
        PrintVolumeFit fit)
    {
        Profile = profile;
        FrameLines = frameLines;
        FloorGridLines = floorGridLines;
        FloorGridStepMm = floorGridStepMm;
        OutOfVolumeTriangles = outOfVolumeTriangles;
        Fit = fit;
    }

    /// <summary>Профиль принтера, для которого построен оверлей.</summary>
    public PrinterProfile Profile { get; }

    /// <summary>Рёбра каркаса зоны печати (пары вершин под GL_LINES).</summary>
    public float[] FrameLines { get; }

    /// <summary>Линии сетки пола внутри зоны (пары вершин под GL_LINES).</summary>
    public float[] FloorGridLines { get; }

    /// <summary>Шаг сетки пола, мм.</summary>
    public double FloorGridStepMm { get; }

    /// <summary>Треугольники модели за пределами зоны (9 float на треугольник).</summary>
    public float[] OutOfVolumeTriangles { get; }

    /// <summary>Попадание модели в зону печати.</summary>
    public PrintVolumeFit Fit { get; }

    /// <summary>Есть ли геометрия за пределами зоны (подсветка).</summary>
    public bool HasOutOfVolume => OutOfVolumeTriangles.Length > 0;

    /// <summary>Построить оверлей для профиля и (опционально) модели сцены.</summary>
    public static PrintVolumeOverlay Build(PrinterProfile profile, SceneRenderModel? model)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Core.Geometry.BoundingBox3d volume = profile.BuildVolumeBounds;
        float[] frame = PrintVolumeGeometry.CreateFrameLines(volume);
        float[] floor = PrintVolumeGeometry.CreateFloorGridLines(volume, out double step);
        IReadOnlyList<RenderObject> objects = model?.Objects ?? [];
        float[] outOfVolume = PrintVolumeGeometry.CreateOutOfVolumeTriangles(objects, volume);
        PrintVolumeFit fit = PrintVolumeGeometry.AnalyzeFit(objects, volume);
        return new PrintVolumeOverlay(profile, frame, floor, step, outOfVolume, fit);
    }
}
