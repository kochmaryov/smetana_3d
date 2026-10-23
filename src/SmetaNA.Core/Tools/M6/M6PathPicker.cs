using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.M6;

/// <summary>Попадание в ребро (проекция на экран, мировые концы).</summary>
public readonly record struct M6EdgeHit(Vector3d From, Vector3d To, double DistancePx);

/// <summary>
/// Выбор рёбер сцены для траектории Follow Me (задача 5.2): ближайшее к
/// курсору ребро любого меша (включая проволочные рёбра эскизов) в пиксельном
/// допуске. Связность цепочки проверяется по мировым концам рёбер — путь может
/// переходить между объектами.
/// </summary>
public static class M6PathPicker
{
    /// <summary>Пиксельный допуск захвата ребра.</summary>
    public const double DefaultTolerancePx = 10.0;

    /// <summary>Допуск совпадения концов рёбер при построении цепочки, мм.</summary>
    public const double ChainToleranceMm = 1e-6;

    /// <summary>Ближайшее к курсору ребро сцены (мировые концы); <c>false</c> — в допуске нет.</summary>
    public static bool TryPickEdge(
        Scene scene,
        InferenceViewport viewport,
        Vector2d cursorPx,
        out M6EdgeHit hit,
        double tolerancePx = DefaultTolerancePx)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(viewport);
        hit = default;

        bool found = false;
        double best = tolerancePx;
        foreach (PlacedNode placed in SceneFlattener.Expand(scene))
        {
            if (placed.Node is not MeshNode meshNode)
            {
                continue;
            }

            var processed = new HashSet<MeshHalfEdge>();
            foreach (MeshHalfEdge edge in meshNode.Mesh.HalfEdges)
            {
                if (edge.Next is not { } next)
                {
                    continue;
                }

                if (edge.Twin is { } twin && !processed.Add(twin))
                {
                    continue;
                }

                processed.Add(edge);

                Vector3d from = placed.WorldTransform.TransformPoint(edge.Origin.Position);
                Vector3d to = placed.WorldTransform.TransformPoint(next.Origin.Position);
                if (viewport.TryProject(from) is not { } fromPx || viewport.TryProject(to) is not { } toPx)
                {
                    continue;
                }

                double distance = DistanceToSegment(cursorPx, fromPx, toPx);
                if (distance <= best)
                {
                    best = distance;
                    hit = new M6EdgeHit(from, to, distance);
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>Расстояние от точки до отрезка (пиксели).</summary>
    public static double DistanceToSegment(Vector2d point, Vector2d from, Vector2d to)
    {
        Vector2d segment = to - from;
        double lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared <= 0)
        {
            return Vector2d.Distance(point, from);
        }

        double t = ((point.X - from.X) * segment.X + (point.Y - from.Y) * segment.Y) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var projection = new Vector2d(from.X + (segment.X * t), from.Y + (segment.Y * t));
        return Vector2d.Distance(point, projection);
    }

    /// <summary>Совпадение мировых точек (стыковка цепочки рёбер).</summary>
    public static bool SamePoint(Vector3d a, Vector3d b)
        => (a - b).Length() <= ChainToleranceMm;
}
