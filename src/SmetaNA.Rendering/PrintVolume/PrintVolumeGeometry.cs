using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering.PrintVolume;

/// <summary>
/// Попадание модели в зону печати (задача 4.5): превышение габаритов по осям
/// и число треугольников за пределами зоны (для подсветки вьюпорта).
/// </summary>
/// <param name="Fits">Модель целиком помещается в зону печати.</param>
/// <param name="OverflowXMm">Максимальное превышение по X (влево/вправо), мм.</param>
/// <param name="OverflowYMm">Максимальное превышение по Y (вперёд/назад), мм.</param>
/// <param name="OverflowZMm">Максимальное превышение по высоте Z, мм.</param>
/// <param name="OutOfVolumeTriangleCount">Треугольников с вершинами за пределами зоны.</param>
public sealed record PrintVolumeFit(
    bool Fits,
    double OverflowXMm,
    double OverflowYMm,
    double OverflowZMm,
    int OutOfVolumeTriangleCount);

/// <summary>
/// Геометрия зоны печати для GL-оверлея вьюпорта: каркас зоны, сетка пола
/// внутри стола и треугольники модели «за пределами зоны». Линии задаются
/// парами вершин (x,y,z) под GL_LINES, треугольники — тройками вершин.
/// </summary>
public static class PrintVolumeGeometry
{
    private const int MaxLinesPerAxis = 40;

    /// <summary>Каркас зоны печати: 12 рёбер параллелепипеда.</summary>
    public static float[] CreateFrameLines(BoundingBox3d volume)
    {
        EnsureNotEmpty(volume);
        Vector3d min = volume.Min;
        Vector3d max = volume.Max;

        // Восемь углов: индекс бита = (x, y, z) максимума по осям.
        var corners = new float[8 * 3];
        for (int corner = 0; corner < 8; corner++)
        {
            corners[(corner * 3) + 0] = (float)((corner & 1) == 0 ? min.X : max.X);
            corners[(corner * 3) + 1] = (float)((corner & 2) == 0 ? min.Y : max.Y);
            corners[(corner * 3) + 2] = (float)((corner & 4) == 0 ? min.Z : max.Z);
        }

        int[] edges =
        [
            0, 1, 2, 3, 4, 5, 6, 7, // рёбра вдоль X
            0, 2, 1, 3, 4, 6, 5, 7, // рёбра вдоль Y
            0, 4, 1, 5, 2, 6, 3, 7, // рёбра вдоль Z
        ];

        var lines = new float[12 * 2 * 3];
        for (int edge = 0; edge < 12; edge++)
        {
            int from = edges[edge * 2] * 3;
            int to = edges[edge * 2 + 1] * 3;
            lines[edge * 6 + 0] = corners[from + 0];
            lines[edge * 6 + 1] = corners[from + 1];
            lines[edge * 6 + 2] = corners[from + 2];
            lines[edge * 6 + 3] = corners[to + 0];
            lines[edge * 6 + 4] = corners[to + 1];
            lines[edge * 6 + 5] = corners[to + 2];
        }

        return lines;
    }

    /// <summary>
    /// Сетка пола внутри зоны печати (плоскость Z = низ зоны) с «круглым» шагом
    /// (1/2/5 × 10ⁿ мм); шаг возвращается через <paramref name="stepMm"/>.
    /// </summary>
    public static float[] CreateFloorGridLines(BoundingBox3d volume, out double stepMm)
    {
        EnsureNotEmpty(volume);
        stepMm = NiceStep(volume);

        double extent = Math.Max(volume.Size.X, volume.Size.Y);
        double halfExtent = extent * 0.5 + (stepMm * 0.001);
        int lineCount = Math.Min((int)Math.Ceiling(halfExtent / stepMm), MaxLinesPerAxis);
        double z = volume.Min.Z;
        var lines = new List<float>((lineCount * 2 + 1) * 2 * 6);

        for (int i = -lineCount; i <= lineCount; i++)
        {
            double offset = i * stepMm;
            if (Math.Abs(offset) > halfExtent)
            {
                continue;
            }

            AddLine(lines, offset, -halfExtent, offset, halfExtent, z);
            AddLine(lines, -halfExtent, offset, halfExtent, offset, z);
        }

        return [.. lines];
    }

    /// <summary>
    /// Треугольники модели с вершинами за пределами зоны печати (подсветка
    /// «за пределами зоны»): 9 float на треугольник, порядок — как в
    /// <see cref="SmetaNA.Rendering.RenderObject.Triangles"/>.
    /// </summary>
    public static float[] CreateOutOfVolumeTriangles(IReadOnlyList<RenderObject> objects, BoundingBox3d volume)
    {
        ArgumentNullException.ThrowIfNull(objects);
        EnsureNotEmpty(volume);

        var triangles = new List<float>();
        foreach (RenderObject obj in objects)
        {
            MeshData mesh = obj.Triangles;
            IReadOnlyList<Vector3d> positions = mesh.Positions;
            IReadOnlyList<int> indices = mesh.Indices;
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                Vector3d a = positions[indices[i]];
                Vector3d b = positions[indices[i + 1]];
                Vector3d c = positions[indices[i + 2]];
                if (IsInside(volume, a) && IsInside(volume, b) && IsInside(volume, c))
                {
                    continue;
                }

                AppendVertex(triangles, a);
                AppendVertex(triangles, b);
                AppendVertex(triangles, c);
            }
        }

        return [.. triangles];
    }

    /// <summary>
    /// Попадание модели в зону печати: превышение габаритов по осям (по bounding
    /// box модели) и число треугольников за пределами зоны.
    /// </summary>
    public static PrintVolumeFit AnalyzeFit(IReadOnlyList<RenderObject> objects, BoundingBox3d volume)
    {
        ArgumentNullException.ThrowIfNull(objects);
        EnsureNotEmpty(volume);

        BoundingBox3d bounds = BoundingBox3d.Empty;
        int outOfVolume = 0;
        foreach (RenderObject obj in objects)
        {
            bounds = bounds.Union(obj.Bounds);
            MeshData mesh = obj.Triangles;
            IReadOnlyList<Vector3d> positions = mesh.Positions;
            IReadOnlyList<int> indices = mesh.Indices;
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                if (!IsInside(volume, positions[indices[i]])
                    || !IsInside(volume, positions[indices[i + 1]])
                    || !IsInside(volume, positions[indices[i + 2]]))
                {
                    outOfVolume++;
                }
            }
        }

        if (bounds.IsEmpty)
        {
            return new PrintVolumeFit(true, 0, 0, 0, 0);
        }

        double overflowX = Overflow(bounds.Min.X, bounds.Max.X, volume.Min.X, volume.Max.X);
        double overflowY = Overflow(bounds.Min.Y, bounds.Max.Y, volume.Min.Y, volume.Max.Y);
        double overflowZ = Overflow(bounds.Min.Z, bounds.Max.Z, volume.Min.Z, volume.Max.Z);
        bool fits = overflowX <= 0 && overflowY <= 0 && overflowZ <= 0;
        return new PrintVolumeFit(fits, overflowX, overflowY, overflowZ, outOfVolume);
    }

    /// <summary>«Круглый» шаг сетки (1/2/5 × 10ⁿ мм) по габаритам зоны.</summary>
    public static double NiceStep(BoundingBox3d volume)
    {
        EnsureNotEmpty(volume);
        double extent = Math.Max(volume.Size.X, volume.Size.Y);
        double raw = extent / 10.0;
        double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(Math.Max(raw, 0.1))));
        double normalized = raw / magnitude;
        double nice = normalized >= 5 ? 5 : normalized >= 2 ? 2 : 1;
        return nice * magnitude;
    }

    private static double Overflow(double min, double max, double volumeMin, double volumeMax) =>
        Math.Max(volumeMin - min, max - volumeMax);

    private static bool IsInside(BoundingBox3d volume, Vector3d point) =>
        point.X >= volume.Min.X && point.X <= volume.Max.X
        && point.Y >= volume.Min.Y && point.Y <= volume.Max.Y
        && point.Z >= volume.Min.Z && point.Z <= volume.Max.Z;

    private static void AppendVertex(List<float> target, Vector3d point)
    {
        target.Add((float)point.X);
        target.Add((float)point.Y);
        target.Add((float)point.Z);
    }

    private static void AddLine(List<float> target, double x1, double y1, double x2, double y2, double z)
    {
        AppendVertex(target, new Vector3d(x1, y1, z));
        AppendVertex(target, new Vector3d(x2, y2, z));
    }

    private static void EnsureNotEmpty(BoundingBox3d volume)
    {
        if (volume.IsEmpty)
        {
            throw new ArgumentException("Зона печати не задана (пустой bounding box).", nameof(volume));
        }
    }
}
