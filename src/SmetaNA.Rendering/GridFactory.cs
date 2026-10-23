using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Геометрия сетки (плоскость Z=0) и мировых осей для вьюпорта.
/// Линии задаются парами вершин (x,y,z) под рисование GL_LINES.
/// </summary>
public static class GridFactory
{
    private const double MinCell = 1.0; // мм
    private const int MaxLinesPerAxis = 60;

    /// <summary>«Круглый» шаг сетки (1/2/5 × 10ⁿ мм) по размеру сцены.</summary>
    public static double NiceCellSize(BoundingBox3d bounds)
    {
        double extent = Math.Max(
            Math.Max(bounds.IsEmpty ? 0 : bounds.Size.X, bounds.IsEmpty ? 0 : bounds.Size.Y),
            10_000.0);
        double raw = extent / 10.0;
        double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(Math.Max(raw, MinCell))));
        double normalized = raw / magnitude;
        double nice = normalized >= 5 ? 5 : normalized >= 2 ? 2 : 1;
        return Math.Max(nice * magnitude, MinCell);
    }

    /// <summary>Сегменты сетки на плоскости Z=0, покрывающие bounding box сцены.</summary>
    public static float[] CreateGridLines(BoundingBox3d bounds, out double cell)
    {
        cell = NiceCellSize(bounds);

        double centerX = bounds.IsEmpty ? 0 : bounds.Center.X;
        double centerY = bounds.IsEmpty ? 0 : bounds.Center.Y;
        double halfExtent = Math.Max(cell * 5.0, (bounds.IsEmpty ? 10_000.0 : Math.Max(bounds.Size.X, bounds.Size.Y)) * 0.75);
        double step = cell;
        while ((2.0 * halfExtent / step) > MaxLinesPerAxis)
        {
            step *= 2.0;
        }

        cell = step;
        int lineCount = (int)Math.Ceiling((2.0 * halfExtent) / step);
        var lines = new List<float>((lineCount + 1) * 2 * 6);

        void AddLine(double x1, double y1, double x2, double y2)
        {
            lines.Add((float)x1);
            lines.Add((float)y1);
            lines.Add(0f);
            lines.Add((float)x2);
            lines.Add((float)y2);
            lines.Add(0f);
        }

        for (int i = -lineCount; i <= lineCount; i++)
        {
            double offset = i * step;
            AddLine(centerX + offset, centerY - halfExtent, centerX + offset, centerY + halfExtent);
            AddLine(centerX - halfExtent, centerY + offset, centerX + halfExtent, centerY + offset);
        }

        return [.. lines];
    }

    /// <summary>
    /// Три сегмента осей от начала координат: X (красная), Y (зелёная), Z (синяя).
    /// Порядок фиксирован — рендер красит каждый сегмент своим цветом.
    /// </summary>
    public static float[] CreateAxisLines(BoundingBox3d bounds, out double length)
    {
        length = Math.Max(bounds.IsEmpty ? 5_000.0 : bounds.DiagonalLength * 0.35, 1_000.0);
        float l = (float)length;
        return
        [
            0f, 0f, 0f, l, 0f, 0f,
            0f, 0f, 0f, 0f, l, 0f,
            0f, 0f, 0f, 0f, 0f, l,
        ];
    }
}
