using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools;

/// <summary>Попадание в грань меша.</summary>
/// <param name="Node">Узел сцены с мешем.</param>
/// <param name="Face">Грань меша.</param>
/// <param name="Point">Точка попадания в мировых координатах.</param>
/// <param name="LocalPoint">Точка попадания в локальных координатах узла.</param>
/// <param name="WorldTransform">Мировая трансформация узла.</param>
/// <param name="Distance">Расстояние от начала луча до точки попадания.</param>
public readonly record struct ToolFaceHit(
    MeshNode Node,
    MeshFace Face,
    Vector3d Point,
    Vector3d LocalPoint,
    Transform3d WorldTransform,
    double Distance);

/// <summary>
/// Выбор грани под курсором: луч по сцене → ближайшая грань меша
/// (пересечение плоскости грани + попадание в полигон). Работает в локальных
/// координатах узла через обратную трансформацию, поэтому не зависит от
/// трансформаций иерархии сцены.
/// </summary>
public static class SceneFacePicker
{
    /// <summary>Найти ближайшую грань, пересекаемую лучом.</summary>
    public static bool TryPick(Scene scene, Ray3d worldRay, out ToolFaceHit hit, double maxDistance = double.PositiveInfinity)
    {
        ArgumentNullException.ThrowIfNull(scene);
        hit = default;

        bool found = false;
        double bestDistance = maxDistance;
        foreach (PlacedNode placed in SceneFlattener.Expand(scene))
        {
            if (placed.Node is not MeshNode meshNode)
            {
                continue;
            }

            BoundingBox3d worldBounds = BoundingBox3d.FromMesh(meshNode.Mesh).Transformed(placed.WorldTransform);
            if (!IntersectsBounds(worldRay, worldBounds, bestDistance))
            {
                continue;
            }

            if (!TryInvert(placed.WorldTransform, out Transform3d inverse))
            {
                continue;
            }

            Ray3d localRay = new(
                inverse.TransformPoint(worldRay.Origin),
                inverse.TransformVector(worldRay.Direction));

            if (!TryPickMesh(meshNode.Mesh, localRay, out MeshFace face, out Vector3d localPoint))
            {
                continue;
            }

            Vector3d worldPoint = placed.WorldTransform.TransformPoint(localPoint);
            double distance = (worldPoint - worldRay.Origin).Length();
            if (distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            hit = new ToolFaceHit(
                meshNode,
                face,
                worldPoint,
                localPoint,
                placed.WorldTransform,
                distance);
        }

        return found;
    }

    /// <summary>Перевести мировой луч в локальные координаты узла.</summary>
    public static Ray3d ToLocalRay(Ray3d worldRay, Transform3d worldTransform)
    {
        if (!TryInvert(worldTransform, out Transform3d inverse))
        {
            throw new InvalidOperationException("Трансформация узла необратима.");
        }

        return new Ray3d(
            inverse.TransformPoint(worldRay.Origin),
            inverse.TransformVector(worldRay.Direction));
    }

    /// <summary>Ближайшая грань меша по лучу в локальных координатах.</summary>
    private static bool TryPickMesh(
        HalfEdgeMesh mesh,
        Ray3d localRay,
        out MeshFace pickedFace,
        out Vector3d localPoint)
    {
        pickedFace = null!;
        localPoint = default;
        bool found = false;
        double bestT = double.PositiveInfinity;
        var ring = new List<Vector3d>();

        foreach (MeshFace face in mesh.Faces)
        {
            Vector3d normal = MeshNormals.ComputeFaceNormal(face).Normalized();
            double denominator = Vector3d.Dot(normal, localRay.Direction);
            if (Math.Abs(denominator) <= 1e-12)
            {
                continue;
            }

            double t = Vector3d.Dot(face.HalfEdge.Origin.Position - localRay.Origin, normal) / denominator;
            if (t <= 0 || t >= bestT)
            {
                continue;
            }

            Vector3d candidate = localRay.PointAt(t);
            ring.Clear();
            MeshHalfEdge edge = face.HalfEdge;
            do
            {
                ring.Add(edge.Origin.Position);
                edge = edge.Next!;
            }
            while (edge != face.HalfEdge);

            if (!ContainsProjected(ring, normal, candidate))
            {
                continue;
            }

            found = true;
            bestT = t;
            pickedFace = face;
            localPoint = candidate;
        }

        return found;
    }

    private static bool ContainsProjected(IReadOnlyList<Vector3d> ring, Vector3d normal, Vector3d point)
    {
        double ax = Math.Abs(normal.X);
        double ay = Math.Abs(normal.Y);
        double az = Math.Abs(normal.Z);
        int drop = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2;

        Span<double> xs = stackalloc double[ring.Count];
        Span<double> ys = stackalloc double[ring.Count];
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d p = ring[i];
            xs[i] = drop == 0 ? p.Y : p.X;
            ys[i] = drop == 2 ? p.Y : p.Z;
        }

        double px = drop == 0 ? point.Y : point.X;
        double py = drop == 2 ? point.Y : point.Z;

        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if ((ys[i] > py) != (ys[j] > py)
                && px < ((xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i])) + xs[i])
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IntersectsBounds(Ray3d ray, BoundingBox3d bounds, double maxDistance)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        double tMin = 0;
        double tMax = maxDistance;
        if (!ClipAxis(ray.Origin.X, ray.Direction.X, bounds.Min.X, bounds.Max.X, ref tMin, ref tMax))
        {
            return false;
        }

        if (!ClipAxis(ray.Origin.Y, ray.Direction.Y, bounds.Min.Y, bounds.Max.Y, ref tMin, ref tMax))
        {
            return false;
        }

        return ClipAxis(ray.Origin.Z, ray.Direction.Z, bounds.Min.Z, bounds.Max.Z, ref tMin, ref tMax);
    }

    private static bool ClipAxis(double origin, double direction, double min, double max, ref double tMin, ref double tMax)
    {
        if (Math.Abs(direction) < 1e-15)
        {
            return origin >= min && origin <= max;
        }

        double t1 = (min - origin) / direction;
        double t2 = (max - origin) / direction;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);
        return tMin <= tMax;
    }

    /// <summary>Аффинная обратная трансформация (row-vector-раскладка).</summary>
    private static bool TryInvert(Transform3d transform, out Transform3d inverse)
    {
        inverse = default;

        double a11 = transform.M11, a12 = transform.M12, a13 = transform.M13;
        double a21 = transform.M21, a22 = transform.M22, a23 = transform.M23;
        double a31 = transform.M31, a32 = transform.M32, a33 = transform.M33;

        double determinant =
            (a11 * ((a22 * a33) - (a23 * a32)))
            - (a12 * ((a21 * a33) - (a23 * a31)))
            + (a13 * ((a21 * a32) - (a22 * a31)));

        if (Math.Abs(determinant) < 1e-12)
        {
            return false;
        }

        double inv = 1.0 / determinant;
        double b11 = ((a22 * a33) - (a23 * a32)) * inv;
        double b12 = ((a13 * a32) - (a12 * a33)) * inv;
        double b13 = ((a12 * a23) - (a13 * a22)) * inv;
        double b21 = ((a23 * a31) - (a21 * a33)) * inv;
        double b22 = ((a11 * a33) - (a13 * a31)) * inv;
        double b23 = ((a13 * a21) - (a11 * a23)) * inv;
        double b31 = ((a21 * a32) - (a22 * a31)) * inv;
        double b32 = ((a12 * a31) - (a11 * a32)) * inv;
        double b33 = ((a11 * a22) - (a12 * a21)) * inv;

        double t1 = transform.M41, t2 = transform.M42, t3 = transform.M43;
        inverse = new Transform3d(
            b11, b12, b13, 0,
            b21, b22, b23, 0,
            b31, b32, b33, 0,
            -((t1 * b11) + (t2 * b21) + (t3 * b31)),
            -((t1 * b12) + (t2 * b22) + (t3 * b32)),
            -((t1 * b13) + (t2 * b23) + (t3 * b33)),
            1);
        return true;
    }
}
