using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Построение снимка сцены для inference из иерархии <see cref="Scene"/>:
/// мировые рёбра (уникальные неориентированные рёбра мешей) и грани (кольца граней)
/// с учётом трансформаций, групп и инстансов компонентов.
/// Окружности в мешевой геометрии отсутствуют — их добавляют сами инструменты
/// (окружность/дуга) в <see cref="InferenceScene"/> напрямую.
/// </summary>
public static class InferenceSceneBuilder
{
    /// <summary>Снимок всех видимых мешей сцены.</summary>
    public static InferenceScene FromScene(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return FromNode(scene.Root, Transform3d.Identity, includeHidden);
    }

    /// <summary>
    /// Снимок поддерева узла с начальной трансформацией; содержимое определений
    /// компонентов разворачивается по одному разу на каждый инстанс.
    /// </summary>
    public static InferenceScene FromNode(SceneNode root, Transform3d rootTransform, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(root);

        var segments = new List<SnapSegment>();
        var faces = new List<SnapFace>();
        CollectNode(root, rootTransform, includeHidden, [], segments, faces);
        return new InferenceScene(segments, faces);
    }

    private static void CollectNode(
        SceneNode node,
        Transform3d parentWorld,
        bool includeHidden,
        HashSet<Guid> openDefinitions,
        List<SnapSegment> segments,
        List<SnapFace> faces)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);
        if (node is MeshNode meshNode)
        {
            CollectMesh(meshNode.Mesh, world, segments, faces);
        }

        foreach (SceneNode child in EnumerateContent(node, openDefinitions))
        {
            CollectNode(child, world, includeHidden, openDefinitions, segments, faces);
        }
    }

    private static void CollectMesh(
        HalfEdgeMesh mesh,
        Transform3d world,
        List<SnapSegment> segments,
        List<SnapFace> faces)
    {
        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        var seenEdges = new HashSet<(int From, int To)>();
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            MeshVertex origin = edge.Origin;
            MeshVertex destination = edge.Next!.Origin;
            int i = indexOf[origin];
            int j = indexOf[destination];
            var key = i < j ? (i, j) : (j, i);
            if (!seenEdges.Add(key))
            {
                continue;
            }

            Vector3d start = world.TransformPoint(origin.Position);
            Vector3d end = world.TransformPoint(destination.Position);
            if ((end - start).LengthSquared() <= 0)
            {
                continue;
            }

            segments.Add(new SnapSegment(start, end));
        }

        foreach (MeshFace face in mesh.Faces)
        {
            var ring = new List<Vector3d>();
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                Vector3d position = world.TransformPoint(edge.Origin.Position);
                if (ring.Count == 0 || (position - ring[^1]).LengthSquared() > 0)
                {
                    ring.Add(position);
                }
            }

            if (ring.Count > 1 && (ring[0] - ring[^1]).LengthSquared() <= 0)
            {
                ring.RemoveAt(ring.Count - 1);
            }

            if (ring.Count < 3 || ComputeNormalMagnitude(ring) <= 0)
            {
                continue;
            }

            faces.Add(new SnapFace(ring));
        }
    }

    private static double ComputeNormalMagnitude(IReadOnlyList<Vector3d> ring)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d current = ring[i];
            Vector3d next = ring[(i + 1) % ring.Count];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z).Length();
    }

    /// <summary>
    /// Собственные дети узла плюс содержимое определения для инстанса компонента
    /// (циклы компонентов обнаруживаются и приводят к исключению, как в
    /// <see cref="SceneFlattener"/>).
    /// </summary>
    private static IEnumerable<SceneNode> EnumerateContent(SceneNode node, HashSet<Guid> openDefinitions)
    {
        if (node is ComponentInstanceNode instance)
        {
            ComponentDefinition definition = instance.Definition;
            if (!openDefinitions.Add(definition.Id))
            {
                throw new InvalidOperationException($"Циклическая ссылка на компонент «{definition.Name}» в иерархии сцены.");
            }

            yield return definition.Content;

            openDefinitions.Remove(definition.Id);
        }

        foreach (SceneNode child in node.Children)
        {
            yield return child;
        }
    }
}
