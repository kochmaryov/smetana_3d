using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools;

/// <summary>
/// Результат выбора точки размерным инструментом: точка, её источник
/// (привязка или ребро) и ребро, если курсор был над ним.
/// </summary>
/// <param name="Point">Выбранная точка (мировые координаты, мм).</param>
/// <param name="SnapType">Тип сработавшей привязки или null (ребро/запасная точка).</param>
/// <param name="Edge">Ребро под курсором (размер «по ребру») или null.</param>
public readonly record struct DimensionPickResult(Vector3d Point, SnapType? SnapType, SnapSegment? Edge);

/// <summary>
/// Выбор точек и рёбер для размерных инструментов: привязки
/// <see cref="InferenceEngine"/> (концы/середины/оси), ребро под курсором
/// (по экранному допуску) и запасной вариант — точка на плоскости z = 0
/// вдоль луча выбора.
/// </summary>
public static class DimensionPickSupport
{
    /// <summary>Допуск выбора, пикселей (как у привязок).</summary>
    public const double TolerancePx = SnapRequest.DefaultTolerancePx;

    /// <summary>
    /// Сильная точечная привязка: её точка используется напрямую.
    /// <see cref="SnapType.OnFace"/> — слабая (произвольная точка грани): ребро под
    /// курсором важнее.
    /// </summary>
    public static bool IsStrongPointSnap(SnapType type) => type != SnapType.OnFace;

    /// <summary>
    /// Точка под курсором: сильная привязка → её точка; иначе ребро под курсором
    /// (ближайшая точка ребра к лучу выбора); иначе слабая привязка OnFace;
    /// иначе точка на плоскости z = 0.
    /// </summary>
    public static bool TryPickPoint(
        ToolContext context,
        InferenceScene scene,
        Vector2d cursorPx,
        Vector3d? anchor,
        out DimensionPickResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);

        result = default;
        if (context.Viewport is not { } viewport)
        {
            return false;
        }

        SnapResult snap = context.Snap(new SnapRequest
        {
            Ray = viewport.PickRay(cursorPx),
            Viewport = viewport,
            Scene = scene,
            CursorPx = cursorPx,
            TolerancePx = TolerancePx,
            Anchor = anchor,
        });

        SnapCandidate? best = snap.Best;
        if (best is not null && IsStrongPointSnap(best.Type))
        {
            result = new DimensionPickResult(best.Point, best.Type, null);
            return true;
        }

        if (TryPickEdge(viewport, scene, cursorPx, out SnapSegment edge, out Vector3d onEdge))
        {
            result = new DimensionPickResult(onEdge, null, edge);
            return true;
        }

        if (best is not null)
        {
            result = new DimensionPickResult(best.Point, best.Type, null);
            return true;
        }

        Ray3d ray = viewport.PickRay(cursorPx);
        result = new DimensionPickResult(FallbackPoint(ray), null, null);
        return true;
    }

    /// <summary>
    /// Ребро под курсором: проекция сегмента в пиксели вьюпорта ближе к курсору
    /// на <paramref name="tolerancePx"/>; <paramref name="closestPoint"/> — точка
    /// ребра, ближайшая к лучу выбора.
    /// </summary>
    public static bool TryPickEdge(
        InferenceViewport viewport,
        InferenceScene scene,
        Vector2d cursorPx,
        out SnapSegment edge,
        out Vector3d closestPoint,
        double tolerancePx = TolerancePx)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(scene);

        edge = default;
        closestPoint = default;
        Ray3d ray = viewport.PickRay(cursorPx);

        SnapSegment bestSegment = default;
        Vector3d bestPoint = default;
        double bestScreenDistance = double.PositiveInfinity;
        double bestDepth = double.PositiveInfinity;

        foreach (SnapSegment segment in scene.Segments)
        {
            Vector2d? startPx = viewport.TryProject(segment.Start);
            Vector2d? endPx = viewport.TryProject(segment.End);
            if (startPx is not { } a || endPx is not { } b)
            {
                continue;
            }

            double screenDistance = DistanceToSegmentPx(cursorPx, a, b);
            if (screenDistance > tolerancePx || screenDistance > bestScreenDistance)
            {
                continue;
            }

            double depth = viewport.DepthOf(segment.Midpoint);
            if (screenDistance == bestScreenDistance && depth >= bestDepth)
            {
                continue;
            }

            bestSegment = segment;
            bestPoint = ClosestPointOnSegment(segment, ray);
            bestScreenDistance = screenDistance;
            bestDepth = depth;
        }

        if (double.IsPositiveInfinity(bestScreenDistance))
        {
            return false;
        }

        edge = bestSegment;
        closestPoint = bestPoint;
        return true;
    }

    /// <summary>Точка ребра, ближайшая к лучу выбора.</summary>
    public static Vector3d ClosestPointOnSegment(SnapSegment segment, Ray3d ray)
    {
        Vector3d direction = segment.End - segment.Start;
        double lengthSquared = direction.LengthSquared();
        if (lengthSquared <= 0)
        {
            return segment.Start;
        }

        // Параметры ближайших точек луча и отрезка (классика «ближайших точек двух прямых»).
        Vector3d w0 = ray.Origin - segment.Start;
        double a = 1.0;
        double b = Vector3d.Dot(ray.Direction, direction);
        double c = lengthSquared;
        double d = Vector3d.Dot(ray.Direction, w0);
        double e = Vector3d.Dot(direction, w0);
        double denominator = a * c - b * b;
        double rayParameter = denominator > 1e-12 ? (b * e - c * d) / denominator : -d;
        double segmentParameter = denominator > 1e-12 ? (a * e - b * d) / denominator : e / c;

        segmentParameter = Math.Clamp(segmentParameter, 0, 1);
        if (denominator <= 1e-12 || rayParameter < 0)
        {
            // Параллельные/вырожденные случаи: ближайшая точка отрезка к точке на луче.
            Vector3d nearestOnRay = ray.PointAt(Math.Max(ray.ClosestParameterTo(segment.Start), 0));
            segmentParameter = Math.Clamp(Vector3d.Dot(nearestOnRay - segment.Start, direction) / lengthSquared, 0, 1);
        }

        return segment.Start + direction * segmentParameter;
    }

    /// <summary>Запасная точка: пересечение луча с плоскостью z = 0 или точка на фиксированной глубине.</summary>
    public static Vector3d FallbackPoint(Ray3d ray)
    {
        if (Math.Abs(ray.Direction.Z) > 1e-9)
        {
            double t = -ray.Origin.Z / ray.Direction.Z;
            if (t > 0 && double.IsFinite(t))
            {
                return ray.PointAt(t);
            }
        }

        return ray.PointAt(1000.0);
    }

    private static double DistanceToSegmentPx(Vector2d point, Vector2d start, Vector2d end)
    {
        Vector2d segment = end - start;
        double lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        double t = lengthSquared <= 0
            ? 0
            : Math.Clamp(((point.X - start.X) * segment.X + (point.Y - start.Y) * segment.Y) / lengthSquared, 0, 1);
        double dx = point.X - (start.X + segment.X * t);
        double dy = point.Y - (start.Y + segment.Y * t);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
