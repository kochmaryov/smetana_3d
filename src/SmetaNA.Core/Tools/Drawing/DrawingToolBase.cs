using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Общая механика инструментов рисования (задача 2.1): плоскость построения
/// (грань под курсором или «земля» XY), разрешение точки через
/// <see cref="InferenceEngine"/> (привязки) с откатом на пересечение луча с
/// плоскостью, коммит фигуры командой истории в координатах целевого меша.
/// <para>
/// Цель коммита: если первый клик попал в грань — её меш (геометрия рисуется
/// в координатах узла); иначе создаётся новый меш-узел сцены. Каждая завершённая
/// фигура — одна команда в <c>UndoStack</c> (отмена/повтор работают).
/// </para>
/// </summary>
public abstract class DrawingToolBase : ToolBase
{
    /// <summary>Минимальная длина сегмента, мм.</summary>
    protected const double MinSegmentLengthMm = 1e-6;

    /// <summary>Радиус «захвата» начальной точки контура для замыкания, пикселей.</summary>
    protected const double CloseTolerancePx = SnapRequest.DefaultTolerancePx;

    private DrawingPlane _plane = DrawingPlane.Ground;
    private bool _surfaceResolved;
    private MeshNode? _targetNode;
    private Transform3d _world = Transform3d.Identity;
    private Transform3d _worldToLocal = Transform3d.Identity;

    /// <summary>Плоскость построения (фиксируется первым кликом фигуры).</summary>
    protected DrawingPlane Plane => _plane;

    /// <summary>Плоскость и цель выбраны первым кликом фигуры.</summary>
    protected bool IsSurfaceResolved => _surfaceResolved;

    /// <summary>Вьюпорт контекста (для лучей и пиксельных допусков); <c>null</c> без UI.</summary>
    protected InferenceViewport? Viewport => Context.Viewport;

    /// <summary>Клик с Shift — панорама камеры, инструмент не перехватывает.</summary>
    protected static bool IsPan(PointerInput input)
        => input.Kind == PointerEventKind.Press && (input.Modifiers & ToolModifiers.Shift) != 0;

    /// <summary>
    /// Определить плоскость и цель построения по пикселю первого клика:
    /// грань под курсором (её плоскость и меш) либо «земля» XY и новый узел.
    /// </summary>
    protected void ResolveSurface(Vector2d cursorPx)
    {
        if (Context.TryPickFace(cursorPx, out ToolFaceHit hit)
            && TransformInverter.TryInvert(hit.WorldTransform, out Transform3d worldToLocal))
        {
            _targetNode = hit.Node;
            _world = hit.WorldTransform;
            _worldToLocal = worldToLocal;

            Vector3d normal = _world.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
            Vector3d origin = _world.TransformPoint(MeshEditOps.FaceCentroid(hit.Face));
            IReadOnlyList<MeshVertex> ring = hit.Node.Mesh.GetRing(hit.Face);
            Vector3d? uHint = ring.Count >= 2
                ? _world.TransformVector(ring[1].Position - ring[0].Position)
                : null;
            _plane = DrawingPlane.Create(origin, normal, uHint);
        }
        else
        {
            _targetNode = null;
            _world = Transform3d.Identity;
            _worldToLocal = Transform3d.Identity;
            _plane = DrawingPlane.Ground;
        }

        _surfaceResolved = true;
    }

    /// <summary>Сброс плоскости/цели (новая фигура определяет их заново).</summary>
    protected void ResetSurface()
    {
        _surfaceResolved = false;
        _targetNode = null;
        _world = Transform3d.Identity;
        _worldToLocal = Transform3d.Identity;
        _plane = DrawingPlane.Ground;
    }

    /// <summary>
    /// Точка построения по курсору: лучший кандидат привязок
    /// (<see cref="ToolContext.Snap"/>: концы, середины, оси, перпендикуляры,
    /// касательные — по запросу с якорем) либо пересечение луча с плоскостью.
    /// </summary>
    protected Vector3d ResolvePoint(
        Vector2d cursorPx,
        Vector3d? anchor,
        ToolModifiers modifiers = ToolModifiers.None)
        => ResolvePoint(cursorPx, anchor, modifiers, [], []);

    /// <summary>
    /// Точка построения по курсору с дополнительными сущностями привязок:
    /// строящиеся окружности/дуги (<paramref name="extraCircles"/>) и рёбра
    /// (<paramref name="extraSegments"/>) входят в снимок сцены запроса.
    /// </summary>
    protected Vector3d ResolvePoint(
        Vector2d cursorPx,
        Vector3d? anchor,
        ToolModifiers modifiers,
        IReadOnlyList<SnapCircle> extraCircles,
        IReadOnlyList<SnapSegment> extraSegments)
    {
        if (Context.Viewport is not { } viewport)
        {
            return _plane.Origin;
        }

        Ray3d ray = viewport.PickRay(cursorPx);
        InferenceScene scene = BuildSnapScene(extraCircles, extraSegments);
        SnapRequest request = new()
        {
            Ray = ray,
            Viewport = viewport,
            Scene = scene,
            CursorPx = cursorPx,
            Anchor = anchor,
            Modifiers = (modifiers & ToolModifiers.Ctrl) != 0
                ? SnapModifiers.PointsOnly
                : SnapModifiers.None,
        };

        if (Context.Snap(request).Best is { } best)
        {
            return best.Point;
        }

        return _plane.TryIntersect(ray, out Vector3d point) ? point : _plane.Origin;
    }

    /// <summary>Проекция точки на плоскость построения (фигуры — плоские).</summary>
    protected Vector3d OnPlane(Vector3d point) => _plane.Project(point);

    /// <summary>Курсор рядом с мировой точкой (пиксельный допуск — замыкание контура).</summary>
    protected bool IsNearInPixels(Vector2d cursorPx, Vector3d worldPoint, double tolerancePx = CloseTolerancePx)
        => Context.Viewport is { } viewport
            && viewport.TryProject(worldPoint) is { } projected
            && Vector2d.Distance(projected, cursorPx) <= tolerancePx;

    /// <summary>
    /// Коммит фигуры одной командой истории: в меш цели (первый клик попал в
    /// грань) либо в новый меш-узел сцены.
    /// </summary>
    protected void CommitShape(string description, string nodeName, DrawingShapeSpec spec)
    {
        if (_targetNode is { } target)
        {
            Context.Execute(new AddDrawingShapeCommand(target.Mesh, spec.Transform(_worldToLocal), description));
            return;
        }

        var mesh = new HalfEdgeMesh();
        var node = new MeshNode { Mesh = mesh, Name = nodeName };
        Context.Execute(new CompositeEditCommand(
            description,
            [
                new AddNodeCommand(Context.Scene.Root, node),
                new AddDrawingShapeCommand(mesh, spec, description),
            ]));
    }

    /// <summary>Подпись предпросмотра у мировой точки.</summary>
    protected static ToolPreviewLabel LabelAt(Vector3d anchor, string text) => new(anchor, text);

    /// <summary>Размер в миллиметрах для подписи/описания (инвариантный формат).</summary>
    protected static string FormatMm(double value)
        => string.Format(CultureInfo.InvariantCulture, "{0:0.###} mm", value);

    /// <summary>Линии предпросмотра по точкам (с замыканием кольца по запросу).</summary>
    protected static void AppendLines(
        List<ToolPreviewLine> lines,
        IReadOnlyList<Vector3d> points,
        ToolPreviewStyle style,
        bool close = false)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            lines.Add(new ToolPreviewLine(points[i], points[i + 1], style));
        }

        if (close && points.Count > 2)
        {
            lines.Add(new ToolPreviewLine(points[^1], points[0], style));
        }
    }

    private InferenceScene BuildSnapScene(
        IReadOnlyList<SnapCircle> extraCircles,
        IReadOnlyList<SnapSegment> extraSegments)
    {
        InferenceScene scene = Context.BuildInferenceScene();
        if (extraCircles.Count == 0 && extraSegments.Count == 0)
        {
            return scene;
        }

        var segments = new List<SnapSegment>(scene.Segments);
        segments.AddRange(extraSegments);
        var circles = new List<SnapCircle>(scene.Circles);
        circles.AddRange(extraCircles);
        return new InferenceScene(segments, scene.Faces, circles);
    }
}
