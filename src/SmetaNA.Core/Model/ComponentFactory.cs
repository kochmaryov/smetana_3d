namespace SmetaNA.Core.Model;

/// <summary>
/// Фабрика компонентов (задача 3.4): инстансы (общее определение — наследование
/// правок мастера) и независимые копии (своё определение — без наследования).
/// </summary>
public static class ComponentFactory
{
    /// <summary>Инстанс: все инстансы одного определения разделяют мастер-контент.</summary>
    public static ComponentInstanceNode CreateInstance(ComponentDefinition definition, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ComponentInstanceNode
        {
            Definition = definition,
            Name = name ?? definition.Name,
        };
    }

    /// <summary>Копия определения: новый мастер-контент (глубокий клон) с тем же именем.</summary>
    public static ComponentDefinition CloneDefinition(ComponentDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ComponentDefinition
        {
            Name = source.Name,
            Content = NodeCloner.DeepCopy(source.Content),
        };
    }

    /// <summary>
    /// Независимая копия инстанса: своё определение с клоном мастер-контента
    /// и клонами собственных детей; вложенные инстансы остаются общими.
    /// </summary>
    public static ComponentInstanceNode CreateIndependentCopy(ComponentInstanceNode source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copy = new ComponentInstanceNode
        {
            Definition = CloneDefinition(source.Definition),
            Name = source.Name,
            IsVisible = source.IsVisible,
            LocalTransform = source.LocalTransform,
            Tag = source.Tag,
        };
        foreach (SceneNode child in source.Children)
        {
            copy.AddChild(NodeCloner.DeepCopy(child));
        }

        return copy;
    }
}
