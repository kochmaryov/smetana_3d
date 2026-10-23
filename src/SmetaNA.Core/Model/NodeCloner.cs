using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Model;

/// <summary>
/// Глубокое копирование поддеревьев сцены. Вложенные инстансы компонентов
/// копируются «как инстансы»: продолжают ссылаться на те же определения,
/// поэтому наследование правок мастера сохраняется.
/// </summary>
public static class NodeCloner
{
    /// <summary>Глубокая копия поддерева: новые узлы, новый меш, те же определения компонентов.</summary>
    public static SceneNode DeepCopy(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        SceneNode copy = CopyShell(node);
        foreach (SceneNode child in node.Children)
        {
            copy.AddChild(DeepCopy(child));
        }

        return copy;
    }

    private static SceneNode CopyShell(SceneNode node) => node switch
    {
        MeshNode mesh => new MeshNode
        {
            Mesh = MeshTransforms.Transformed(mesh.Mesh, Transform3d.Identity),
            Name = mesh.Name,
            IsVisible = mesh.IsVisible,
            LocalTransform = mesh.LocalTransform,
            Tag = mesh.Tag,
        },
        ComponentInstanceNode instance => new ComponentInstanceNode
        {
            Definition = instance.Definition,
            Name = instance.Name,
            IsVisible = instance.IsVisible,
            LocalTransform = instance.LocalTransform,
            Tag = instance.Tag,
        },
        GroupNode group => new GroupNode
        {
            Name = group.Name,
            IsVisible = group.IsVisible,
            LocalTransform = group.LocalTransform,
            Tag = group.Tag,
        },
        _ => throw new NotSupportedException($"Копирование узлов типа {node.GetType().Name} не поддерживается."),
    };
}
