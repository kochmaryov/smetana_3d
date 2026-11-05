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
    private DrawingPlane? _lockedPlane;
    private MeshNode? _lockedNode;
    private Transform3d _lockedWorld = Transform3d.Identity;
    private Transform3d _lockedWorldToLocal = Transform3d.Identity;

    /// <summary>Плоскость построения (фиксируется первым кликом фигуры).</summary>
    protected DrawingPlane Plane => _plane;

    /// <summary>Плоскость и цель выбраны первым кликом фигуры.</summary>
    protected bool IsSurfaceResolved => _surfaceResolved;

    /// <summary>Вьюпорт контекста (для лучей и пиксельных допусков); <c>null</c> без UI.</summary>
    protected InferenceViewport? Viewport => Context.Viewport;

    /// <summary>
    /// Клик с Shift — панорама камеры, инструмент не перехватывает. Shift+Alt —
    /// фиксация плоскости построения (R-23), Shift+Ctrl — скрытая геометрия (R-10):
    /// это модификаторы инструмента, панорамой не считаются.
    /// </summary>
    protected static bool IsPan(PointerInput input)
        => input.Kind == PointerEventKind.Press
            && (input.Modifiers & ToolModifiers.Shift) != 0
            && (input.Modifiers & (ToolModifiers.Alt | ToolModifiers.Ctrl)) == 0;

    /// <summary>
    /// Определить плоскость и цель построения по пикселю первого клика:
    /// грань под курсором (её плоскость и меш) либо «земля» XY и новый узел.
    /// </summary>
    protected void ResolveSurface(Vector2d cursorPx)
    {
        if (_lockedPlane is { } lockedPlane)
        {
            _targetNode = _lockedNode;
            _world = _lockedWorld;
            _worldToLocal = _lockedWorldToLocal;
            _plane = lockedPlane;
            _surfaceResolved = true;
            return;
        }

        if (TryPlaneFromFace(cursorPx, out ToolFaceHit hit, out DrawingPlane plane, out Transform3d worldToLocal))
        {
            _targetNode = hit.Node;
            _world = hit.WorldTransform;
            _worldToLocal = worldToLocal;
            _plane = plane;
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

    /// <summary>
    /// Плоскость грани под курсором с трансформацией узла: для построения «по грани»
    /// и фиксации плоскости Shift+Alt (R-23).
    /// </summary>
    private bool TryPlaneFromFace(
        Vector2d cursorPx,
        out ToolFaceHit hit,
        out DrawingPlane plane,
        out Transform3d worldToLocal)
    {
        plane = DrawingPlane.Ground;
        worldToLocal = Transform3d.Identity;
        if (!Context.TryPickFace(cursorPx, out hit)
            || !TransformInverter.TryInvert(hit.WorldTransform, out worldToLocal))
        {
            return false;
        }

        Vector3d normal = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        Vector3d origin = hit.WorldTransform.TransformPoint(MeshEditOps.FaceCentroid(hit.Face));
        IReadOnlyList<MeshVertex> ring = hit.Node.Mesh.GetRing(hit.Face);
        Vector3d? uHint = ring.Count >= 2
            ? hit.WorldTransform.TransformVector(ring[1].Position - ring[0].Position)
            : null;
        plane = DrawingPlane.Create(origin, normal, uHint);
        return true;
    }

    /// <summary>
    /// Фиксация плоскости построения по наведённой грани (Shift+Alt, R-23): пока
    /// модификатор удержан, построение идёт в плоскости последней наведённой грани,
    /// даже если курсор ушёл с неё.
    /// </summary>
    private void UpdatePlaneLock(Vector2d cursorPx, ToolModifiers modifiers)
    {
        const ToolModifiers planeLockModifiers = ToolModifiers.Shift | ToolModifiers.Alt;
        if ((modifiers & planeLockModifiers) != planeLockModifiers)
        {
            _lockedPlane = null;
            _lockedNode = null;
            _lockedWorld = Transform3d.Identity;
            _lockedWorldToLocal = Transform3d.Identity;
            return;
        }

        if (TryPlaneFromFace(cursorPx, out ToolFaceHit hit, out DrawingPlane plane, out Transform3d worldToLocal))
        {
            _lockedPlane = plane;
            _lockedNode = hit.Node;
            _lockedWorld = hit.WorldTransform;
            _lockedWorldToLocal = worldToLocal;
        }
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
            SetSnap(SnapResult.Empty);
            return _plane.Origin;
        }

        UpdatePlaneLock(cursorPx, modifiers);
        DrawingPlane plane = _lockedPlane ?? _plane;
        Ray3d ray = viewport.PickRay(cursorPx);
        InferenceScene scene = BuildSnapScene(extraCircles, extraSegments);
        bool shiftLock = (modifiers & ToolModifiers.Shift) != 0 && (modifiers & ToolModifiers.Alt) == 0;
        SnapCandidate? locked = shiftLock ? CurrentSnap.Best : null;
        SnapRequest? request = Context.BuildSnapRequest(
            cursorPx,
            anchor,
            modifiers,
            locked,
            grid: Context.GridFor(plane),
            scene: scene,
            previousBest: CurrentSnap.Best,
            previousBestAgeMs: SnapBestAgeMs);
        if (request is null)
        {
            SetSnap(SnapResult.Empty);
            return plane.Origin;
        }

        SnapResult snap = Context.Snap(request);
        SetSnap(snap);
        if (snap.Best is { } best)
        {
            // Плоскость зафиксирована Shift+Alt — точка остаётся в её плоскости.
            Vector3d snapped = _lockedPlane is null ? best.Point : plane.Project(best.Point);
            return IsExactSnap(best.Type) ? snapped : ApplyLengthSnapping(snapped, anchor);
        }

        Vector3d free = plane.TryIntersect(ray, out Vector3d point) ? point : plane.Origin;
        return ApplyLengthSnapping(free, anchor);
    }

    /// <summary>Точные геометрические привязки не округляются привязкой длины.</summary>
    protected static bool IsExactSnap(SnapType type) => type is
        SnapType.Origin or SnapType.InstanceOrigin or SnapType.SnapPoint
        or SnapType.Endpoint or SnapType.Midpoint or SnapType.ArcMidpoint
        or SnapType.Center or SnapType.Intersection or SnapType.Grid;

    /// <summary>
    /// Привязка длины к точности (R-14.1): длина от опорной точки округляется до
    /// шага точности, направление сохраняется.
    /// </summary>
    protected Vector3d ApplyLengthSnapping(Vector3d point, Vector3d? anchor)
    {
        if (!Context.Inference.LengthSnappingEnabled || anchor is not { } origin)
        {
            return point;
        }

        Vector3d delta = point - origin;
        double length = delta.Length();
        if (length <= MinSegmentLengthMm)
        {
            return point;
        }

        double precision = Context.Inference.LengthPrecision;
        double snapped = Math.Round(length / precision) * precision;
        if (snapped <= 0)
        {
            return point;
        }

        return origin + delta * (snapped / length);
    }

    /// <summary>
    /// Разрешить и опубликовать привязку под курсором до первого клика:
    /// инструмент показывает маркеры ещё до начала построения.
    /// </summary>
    protected void PreviewSnapAt(Vector2d cursorPx, ToolModifiers modifiers = ToolModifiers.None)
        => ResolvePoint(cursorPx, anchor: null, modifiers);

    /// <summary>Применить точку из VCB (абсолютную или относительную).</summary>
    public override bool TryApplyPoint(Vector3d point, bool relative)
        => IsActive && TryApplyPointCore(point, relative);

    /// <summary>Шаг построения по координате; <c>false</c> — инструмент не поддерживает.</summary>
    protected virtual bool TryApplyPointCore(Vector3d point, bool relative) => false;

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
        InferenceScene scene = Context.SnapScene;
        if (extraCircles.Count == 0 && extraSegments.Count == 0)
        {
            return scene;
        }

        return scene.WithExtra(extraSegments, extraCircles);
    }
}
