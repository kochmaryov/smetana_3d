using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>Результат пика: объект, треугольник, точка попадания и дистанция.</summary>
public sealed record PickResult(RenderObject Object, int TriangleIndex, Vector3d Point, double Distance);

/// <summary>
/// Контекстный пик на CPU: луч из камеры → ближайший объект сцены.
/// Подход выбран вместо цветового буфера: работает без GL-контекста (юнит-тестируем),
/// детерминирован и в double-точности (DoD §5.4), не зависит от режима отображения
/// и не требует readPixels через ANGLE; bbox-префильтр + Мёллер–Трамбор укладываются
/// в бюджет hover-пиков на демо-сцене 500k треугольников.
/// </summary>
public static class ScenePicker
{
    /// <summary>Ближайшее попадание по объектам модели (или null — промах).</summary>
    public static PickResult? Pick(IReadOnlyList<RenderObject> objects, Ray3d ray, double maxDistance = double.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(objects);

        PickResult? best = null;
        double bestDistance = maxDistance;
        foreach (RenderObject obj in objects)
        {
            if (!RayIntersector.TryIntersectBox(ray, obj.Bounds, out double boxEnter) ||
                boxEnter > bestDistance)
            {
                continue;
            }

            if (TryPickObject(obj, ray, bestDistance, out PickResult? hit) &&
                hit is not null &&
                hit.Distance < bestDistance)
            {
                best = hit;
                bestDistance = hit.Distance;
            }
        }

        return best;
    }

    /// <summary>Ближайшее попадание по одному объекту (или null — промах).</summary>
    public static PickResult? PickObject(RenderObject obj, Ray3d ray, double maxDistance = double.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return TryPickObject(obj, ray, maxDistance, out PickResult? hit) ? hit : null;
    }

    private static bool TryPickObject(RenderObject obj, Ray3d ray, double maxDistance, out PickResult? hit)
    {
        hit = null;
        IReadOnlyList<Vector3d> positions = obj.Triangles.Positions;
        IReadOnlyList<int> indices = obj.Triangles.Indices;
        double best = maxDistance;
        int bestTriangle = -1;
        Vector3d bestPoint = default;

        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3d a = positions[indices[i]];
            Vector3d b = positions[indices[i + 1]];
            Vector3d c = positions[indices[i + 2]];
            if (!RayIntersector.TryIntersectTriangle(ray, a, b, c, out double t, out _, out _) || t >= best)
            {
                continue;
            }

            best = t;
            bestTriangle = i / 3;
            bestPoint = ray.GetPoint(t);
        }

        if (bestTriangle < 0)
        {
            return false;
        }

        hit = new PickResult(obj, bestTriangle, bestPoint, best);
        return true;
    }
}
