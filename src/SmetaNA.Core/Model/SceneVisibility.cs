namespace SmetaNA.Core.Model;

/// <summary>
/// Видимость узлов с учётом тегов (задача 3.5): скрытый узел или скрытый тег
/// скрывает поддерево. Для узлов внутри определений компонентов цепочка родителей
/// ограничена самим определением; видимость «глазами инстанса» считается при обходе
/// (<see cref="OutlinerQuery.Walk(SceneNode)"/> учитывает видимость каждой ветки доступа).
/// </summary>
public static class SceneVisibility
{
    /// <summary>Собственная видимость узла: флаг узла плюс видимость его тега.</summary>
    public static bool IsSelfVisible(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.IsVisible && (node.Tag?.IsVisible ?? true);
    }

    /// <summary>Эффективная видимость: узел и все его предки по цепочке родителей видимы.</summary>
    public static bool IsEffectivelyVisible(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            if (!IsSelfVisible(current))
            {
                return false;
            }
        }

        return true;
    }
}
