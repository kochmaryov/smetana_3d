using SmetaNA.Core.Model;

namespace SmetaNA.Core.Geometry;

/// <summary>Узел сцены вместе с накопленной мировой трансформацией.</summary>
public readonly record struct PlacedNode(SceneNode Node, Transform3d WorldTransform);

/// <summary>
/// Разворот иерархии сцены в плоский вид: мировые трансформации узлов и мировые
/// треугольники (<see cref="MeshData"/>) с учётом вложенности групп и инстансов компонентов.
/// Используется рендером (батчинг) и форматами обмена.
/// </summary>
public static class SceneFlattener
{
    /// <summary>
    /// Все узлы поддерева (включая корневой) с мировыми трансформациями;
    /// содержимое определений компонентов разворачивается по одному разу на каждый инстанс.
    /// Невидимые узлы и их поддеревья пропускаются, если <paramref name="includeHidden"/> = false.
    /// </summary>
    public static IReadOnlyList<PlacedNode> Expand(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var placed = new List<PlacedNode>();
        ExpandNode(scene.Root, Transform3d.Identity, includeHidden, [], placed);
        return placed;
    }

    /// <summary>Мировые треугольники всех мешей сцены.</summary>
    public static MeshData Flatten(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);

        return Flatten(scene.Root, Transform3d.Identity, includeHidden);
    }

    /// <summary>
    /// Мировые треугольники поддерева: позиции вершин каждого меша переводятся в мировые
    /// координаты накопленной трансформацией; вершины между узлами не разделяются
    /// (меш-суп для рендера и обмена).
    /// </summary>
    public static MeshData Flatten(SceneNode root, Transform3d rootTransform, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(root);

        var positions = new List<Vector3d>();
        var indices = new List<int>();
        CollectTriangles(root, rootTransform, includeHidden, [], positions, indices);
        return new MeshData(positions, indices);
    }

    private static void ExpandNode(
        SceneNode node,
        Transform3d parentWorld,
        bool includeHidden,
        HashSet<Guid> openDefinitions,
        List<PlacedNode> placed)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);
        placed.Add(new PlacedNode(node, world));

        foreach (SceneNode child in EnumerateContent(node, openDefinitions))
        {
            ExpandNode(child, world, includeHidden, openDefinitions, placed);
        }
    }

    private static void CollectTriangles(
        SceneNode node,
        Transform3d parentWorld,
        bool includeHidden,
        HashSet<Guid> openDefinitions,
        List<Vector3d> positions,
        List<int> indices)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);
        if (node is MeshNode meshNode)
        {
            MeshData triangles = meshNode.Mesh.ToTriangles();
            int offset = positions.Count;
            foreach (Vector3d position in triangles.Positions)
            {
                positions.Add(world.TransformPoint(position));
            }

            foreach (int index in triangles.Indices)
            {
                indices.Add(offset + index);
            }
        }

        foreach (SceneNode child in EnumerateContent(node, openDefinitions))
        {
            CollectTriangles(child, world, includeHidden, openDefinitions, positions, indices);
        }
    }

    /// <summary>
    /// Собственные дети узла плюс содержимое определения для инстанса компонента.
    /// Тело определения живёт в системе координат инстанса и отражается во все его копии.
    /// Идентификатор определения остаётся «открытым» на время обхода его содержимого —
    /// так обнаруживаются циклические ссылки компонентов.
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
