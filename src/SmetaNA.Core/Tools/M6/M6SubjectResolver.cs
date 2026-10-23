using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Определение субъекта операции M6 (массивы, зеркало): выбранный узел либо
/// узел, которому принадлежит грань под курсором. Грани содержимого определений
/// компонентов «принадлежат» инстансу — операция применяется ко всему инстансу
/// (правка разделяемого содержимого из массива/зеркала не выполняется).
/// </summary>
public static class M6SubjectResolver
{
    /// <summary>
    /// Субъект операции: <paramref name="selected"/> (если жест начался в его
    /// поддереве) либо узел грани под курсором (для инстансов — сам инстанс).
    /// </summary>
    public static SceneNode Resolve(Scene scene, SceneNode? selected, MeshNode hitNode)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(hitNode);
        SceneNode hierarchyNode = HierarchyNodeFor(scene, hitNode);
        return selected is not null && IsSelfOrAncestor(selected, hierarchyNode) ? selected : hierarchyNode;
    }

    /// <summary>Узел иерархии сцены для узла меша (для инстансов — узел инстанса).</summary>
    public static SceneNode HierarchyNodeFor(Scene scene, MeshNode hitNode)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(hitNode);

        SceneNode top = hitNode;
        while (top.Parent is { } parent)
        {
            top = parent;
        }

        if (ReferenceEquals(top, scene.Root))
        {
            return hitNode;
        }

        SceneNode? instance = FindOutermostInstance(scene.Root, hitNode);
        return instance ?? hitNode;
    }

    /// <summary><paramref name="node"/> — это <paramref name="ancestor"/> или его потомок?</summary>
    public static bool IsSelfOrAncestor(SceneNode ancestor, SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(node);
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static ComponentInstanceNode? FindOutermostInstance(SceneNode root, MeshNode target)
    {
        foreach (SceneNode child in root.Children)
        {
            if (child is ComponentInstanceNode instance && ContentContains(instance.Definition.Content, target))
            {
                return instance;
            }

            if (FindOutermostInstance(child, target) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool ContentContains(SceneNode content, MeshNode target)
    {
        if (ReferenceEquals(content, target))
        {
            return true;
        }

        foreach (SceneNode child in M6Transforms.EnumerateContent(content))
        {
            if (ContentContains(child, target))
            {
                return true;
            }
        }

        return false;
    }
}
