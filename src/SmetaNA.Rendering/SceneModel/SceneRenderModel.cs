using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Rendering;

/// <summary>Плоская модель сцены для рендера и пиккинга.</summary>
public sealed class SceneRenderModel
{
    private SceneRenderModel(IReadOnlyList<RenderObject> objects)
    {
        Objects = objects;

        BoundingBox3d bounds = BoundingBox3d.Empty;
        int triangles = 0;
        int edges = 0;
        foreach (RenderObject obj in objects)
        {
            bounds = bounds.Union(obj.Bounds);
            triangles += obj.TriangleCount;
            edges += obj.EdgeCount;
        }

        Bounds = bounds;
        TriangleCount = triangles;
        EdgeCount = edges;
    }

    public IReadOnlyList<RenderObject> Objects { get; }

    public BoundingBox3d Bounds { get; }

    public int TriangleCount { get; }

    public int EdgeCount { get; }

    /// <summary>
    /// Разворот иерархии сцены в объекты рендера: мировые треугольники каждого меша
    /// (представление <see cref="MeshData"/>, как у <see cref="SceneFlattener"/>) плюс
    /// целевые узлы выделения. Своё обходное дерево нужно, потому что содержимое
    /// определения компонента не связано через <see cref="SceneNode.Parent"/> с инстансом
    /// (см. <see cref="SceneFlattener.Expand"/>) — целевой узел выделения отслеживается
    /// по ходу обхода.
    /// </summary>
    public static SceneRenderModel Build(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var objects = new List<RenderObject>();
        foreach (SceneNode child in scene.Root.Children)
        {
            Collect(child, Transform3d.Identity, null, false, includeHidden, [], objects);
        }

        return new SceneRenderModel(objects);
    }

    /// <summary>Модель из готового списка объектов (тесты и внешние источники геометрии).</summary>
    public static SceneRenderModel FromObjects(IReadOnlyList<RenderObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        return new SceneRenderModel(objects);
    }

    private static void Collect(
        SceneNode node,
        Transform3d parentWorld,
        SceneNode? parentTarget,
        bool isDefinitionContent,
        bool includeHidden,
        HashSet<Guid> openDefinitions,
        List<RenderObject> objects)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);
        SceneNode? target =
            (node is GroupNode or ComponentInstanceNode) && !isDefinitionContent
                ? node
                : parentTarget;

        if (node is MeshNode meshNode)
        {
            objects.Add(CreateObject(target ?? node, node, meshNode.Mesh, world));
        }

        if (node is ComponentInstanceNode instance)
        {
            ComponentDefinition definition = instance.Definition;
            if (!openDefinitions.Add(definition.Id))
            {
                throw new InvalidOperationException(
                    $"Циклическая ссылка на компонент «{definition.Name}» в иерархии сцены.");
            }

            // Содержимое определения выделяется как сам инстанс.
            Collect(definition.Content, world, target, true, includeHidden, openDefinitions, objects);
            openDefinitions.Remove(definition.Id);
        }

        foreach (SceneNode child in node.Children)
        {
            Collect(child, world, target, false, includeHidden, openDefinitions, objects);
        }
    }

    private static RenderObject CreateObject(SceneNode selectionNode, SceneNode sourceNode, HalfEdgeMesh mesh, Transform3d world)
    {
        MeshData local = mesh.ToTriangles();
        var positions = new Vector3d[local.Positions.Count];
        for (int i = 0; i < local.Positions.Count; i++)
        {
            positions[i] = world.TransformPoint(local.Positions[i]);
        }

        var indices = new int[local.Indices.Count];
        for (int i = 0; i < local.Indices.Count; i++)
        {
            indices[i] = local.Indices[i];
        }

        var triangles = new MeshData(positions, indices);
        return new RenderObject(
            selectionNode,
            sourceNode,
            triangles,
            EdgeExtractor.FromHalfEdgeMesh(mesh),
            BoundingBox3d.FromMeshData(triangles),
            MaterialColorPath.Build(mesh));
    }
}
