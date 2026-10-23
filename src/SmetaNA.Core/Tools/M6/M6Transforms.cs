using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Трансформации модуля M6 (массивы, зеркало): мировые трансформации узлов,
/// отражение точки/плоскости, композиция локальной трансформации копии и
/// мировые габариты поддерева.
/// <para>
/// Соглашение о композиции — как в <see cref="SceneFlattener"/>: мировая
/// трансформация узла = <c>Multiply(локальная, мировая родителя)</c> (точка
/// сначала проходит локальную трансформацию узла, затем предков).
/// </para>
/// </summary>
public static class M6Transforms
{
    /// <summary>Мировая трансформация узла (композиция предков, соглашение SceneFlattener).</summary>
    public static Transform3d WorldTransformOf(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        // цепочка «локальная ∘ мир родителя»: сначала локальная узла, затем предков
        Transform3d world = Transform3d.Identity;
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            world = Transform3d.Multiply(world, current.LocalTransform);
        }

        return world;
    }

    /// <summary>
    /// Мировая трансформация узла содержимого определения компонента при
    /// заданном размещении инстанса (мир инстанса служит «родителем» содержимого).
    /// </summary>
    public static Transform3d WorldTransformOf(SceneNode node, Transform3d instanceWorld)
    {
        ArgumentNullException.ThrowIfNull(node);
        Transform3d world = Transform3d.Identity;
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            world = Transform3d.Multiply(world, current.LocalTransform);
        }

        return Transform3d.Multiply(world, instanceWorld);
    }

    /// <summary>
    /// Отражение точки относительно плоскости (точка + единичная нормаль):
    /// <c>p' = p − 2·n·(n·(p − P))</c>.
    /// </summary>
    public static Transform3d CreateReflection(Vector3d planePoint, Vector3d planeNormal)
    {
        Vector3d n = planeNormal.Normalized();
        if (n.LengthSquared() <= 0)
        {
            throw new ArgumentException("Нормаль плоскости отражения вырождена.", nameof(planeNormal));
        }

        double nx = n.X;
        double ny = n.Y;
        double nz = n.Z;
        double offset = 2 * Vector3d.Dot(n, planePoint);
        return new Transform3d(
            1 - 2 * nx * nx, -2 * nx * ny, -2 * nx * nz, 0,
            -2 * nx * ny, 1 - 2 * ny * ny, -2 * ny * nz, 0,
            -2 * nx * nz, -2 * ny * nz, 1 - 2 * nz * nz, 0,
            offset * nx, offset * ny, offset * nz, 1);
    }

    /// <summary>
    /// Локальная трансформация копии узла при мировом преобразовании
    /// <paramref name="worldDelta"/> (применяется к мировой точке справа):
    /// <c>L' = W·D·P⁻¹</c>, где W — текущий мир узла, P — мир родителя.
    /// </summary>
    public static bool TryComposeCopyTransform(
        Transform3d nodeWorld,
        Transform3d parentWorld,
        Transform3d worldDelta,
        out Transform3d newLocal)
    {
        newLocal = default;
        if (!Tools.Editing.EditingTransforms.TryInverse(parentWorld, out Transform3d inverseParent))
        {
            return false;
        }

        newLocal = Transform3d.Multiply(
            Transform3d.Multiply(nodeWorld, worldDelta),
            inverseParent);
        return true;
    }

    /// <summary>Мировые габариты поддерева (мешей с учётом трансформаций и инстансов).</summary>
    public static BoundingBox3d WorldBounds(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BoundsIn(node, WorldTransformOf(node));
    }

    private static BoundingBox3d BoundsIn(SceneNode node, Transform3d world)
    {
        BoundingBox3d bounds = BoundingBox3d.Empty;
        if (node is MeshNode meshNode)
        {
            bounds = BoundingBox3d.FromMesh(meshNode.Mesh).Transformed(world);
        }

        foreach (SceneNode child in EnumerateContent(node))
        {
            bounds = bounds.Union(BoundsIn(child, Transform3d.Multiply(child.LocalTransform, world)));
        }

        return bounds;
    }

    /// <summary>
    /// Собственные дети узла плюс содержимое определения для инстанса
    /// (как в <see cref="SceneFlattener"/>).
    /// </summary>
    public static IEnumerable<SceneNode> EnumerateContent(SceneNode node)
    {
        foreach (SceneNode child in node.Children)
        {
            yield return child;
        }

        if (node is ComponentInstanceNode instance)
        {
            yield return instance.Definition.Content;
        }
    }

    /// <summary>Пересечение луча с плоскостью (точка + нормаль).</summary>
    public static bool TryPlaneHit(Inference.Ray3d ray, Vector3d planePoint, Vector3d planeNormal, out Vector3d hit)
        => Tools.Editing.EditingMath.TryIntersectPlane(ray, planePoint, planeNormal, out hit);
}
