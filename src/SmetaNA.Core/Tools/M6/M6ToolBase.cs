using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Общая механика инструментов M6 (массивы, зеркало): разрешение субъекта
/// операции по выборке/клику, запрет панорамы (Shift+ЛКМ) и построение
/// предпросмотра (каркасы габаритов, подписи значений).
/// </summary>
public abstract class M6ToolBase : ToolBase
{
    private SceneNode? _selectionCandidate;

    /// <summary>Выбранный узел на момент активации (объектный режим).</summary>
    protected SceneNode? SelectionCandidate => _selectionCandidate;

    protected override void OnActivated()
    {
        _selectionCandidate = Context.Selection.Node;
    }

    protected override void OnDeactivated()
    {
        _selectionCandidate = null;
    }

    /// <summary>Субъект операции для попадания в грань (выборка либо узел грани).</summary>
    protected SceneNode ResolveSubject(ToolFaceHit hit)
        => M6SubjectResolver.Resolve(Context.Scene, _selectionCandidate, hit.Node);

    /// <summary>Клик с Shift — панорама камеры, инструмент не перехватывает.</summary>
    protected static bool IsPan(PointerInput input)
        => input.Kind == PointerEventKind.Press && (input.Modifiers & ToolModifiers.Shift) != 0;

    /// <summary>12 рёбер габаритного параллелепипеда (мировые координаты).</summary>
    protected static void AppendBounds(
        List<ToolPreviewLine> lines,
        BoundingBox3d bounds,
        ToolPreviewStyle style,
        Transform3d transform)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        Vector3d min = bounds.Min;
        Vector3d max = bounds.Max;
        Vector3d[] corners =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
            new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z),
            new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];

        Span<(int From, int To)> edges =
        [
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        ];

        foreach ((int from, int to) in edges)
        {
            lines.Add(new ToolPreviewLine(
                transform.TransformPoint(corners[from]),
                transform.TransformPoint(corners[to]),
                style));
        }
    }

    /// <summary>Каркас плоскости (ромб вокруг точки с нормалью).</summary>
    protected static void AppendPlane(
        List<ToolPreviewLine> lines,
        Vector3d point,
        Vector3d normal,
        double halfSize,
        ToolPreviewStyle style,
        InferenceViewport? viewport)
    {
        Vector3d n = normal.Normalized();
        if (n.LengthSquared() <= 0)
        {
            return;
        }

        Vector3d u = Editing.EditingMath.ProjectOnPlane(
            viewport?.Forward ?? Vector3d.UnitZ,
            n);
        if (u.LengthSquared() <= 1e-12)
        {
            u = Editing.EditingMath.ProjectOnPlane(Vector3d.UnitX, n);
        }

        if (u.LengthSquared() <= 1e-12)
        {
            return;
        }

        u = u.Normalized();
        Vector3d w = Vector3d.Cross(n, u).Normalized();
        Vector3d a = point + (u * halfSize);
        Vector3d b = point + (w * halfSize);
        Vector3d c = point - (u * halfSize);
        Vector3d d = point - (w * halfSize);
        lines.Add(new ToolPreviewLine(a, b, style));
        lines.Add(new ToolPreviewLine(b, c, style));
        lines.Add(new ToolPreviewLine(c, d, style));
        lines.Add(new ToolPreviewLine(d, a, style));
    }

    /// <summary>Размер в миллиметрах для подписи/описания (инвариантный формат).</summary>
    protected static string FormatMm(double value)
        => string.Format(CultureInfo.InvariantCulture, "{0:0.###} mm", value);

    /// <summary>Угол в градусах для подписи/описания (инвариантный формат).</summary>
    protected static string FormatDeg(double value)
        => string.Format(CultureInfo.InvariantCulture, "{0:0.###}°", value);
}
