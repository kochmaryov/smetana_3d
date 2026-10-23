namespace SmetaNA.Core.Model;

/// <summary>
/// Строка дерева сцены для панели Outliner: узел, глубина и эффективная видимость
/// по ветке доступа (учитывает скрытые инстансы, группы и теги).
/// </summary>
public readonly record struct OutlinerEntry(SceneNode Node, int Depth, bool IsEffectivelyVisible);

/// <summary>
/// Запросы для панели Outliner (задача 3.5): обход дерева с разворачиванием
/// содержимого инстансов компонентов, поиск по имени, цепочка пути до узла.
/// UI поверх этих запросов строится отдельно.
/// </summary>
public static class OutlinerQuery
{
    /// <summary>Обход дерева сцены сверху вниз, начиная с корня.</summary>
    public static IEnumerable<OutlinerEntry> Walk(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return Walk(scene.Root);
    }

    /// <summary>
    /// Обход поддерева сверху вниз (pre-order) с глубиной: контент инстансов
    /// компонентов разворачивается под каждый инстанс, циклические ссылки
    /// определений приводят к исключению. Видимость считается по ветке доступа —
    /// один и тот же узел мастер-контента под разными инстансами виден по-разному.
    /// </summary>
    public static IEnumerable<OutlinerEntry> Walk(SceneNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return WalkNode(root, 0, true, []);
    }

    /// <summary>
    /// Поиск по имени (подстрока без учёта регистра). Пустой шаблон возвращает
    /// всё дерево. Узел мастер-контента встречается по одному разу на каждый инстанс.
    /// </summary>
    public static IReadOnlyList<OutlinerEntry> FindByName(SceneNode root, string name)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(name);

        var matches = new List<OutlinerEntry>();
        foreach (OutlinerEntry entry in Walk(root))
        {
            if (entry.Node.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(entry);
            }
        }

        return matches;
    }

    /// <summary>Поиск по имени по всей сцене.</summary>
    public static IReadOnlyList<OutlinerEntry> FindByName(Scene scene, string name)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return FindByName(scene.Root, name);
    }

    /// <summary>
    /// Путь от вершины иерархии до узла (для «хлебных крошек» Outliner);
    /// для узлов мастер-контента вершина — корень этого контента.
    /// </summary>
    public static IReadOnlyList<SceneNode> GetPath(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var path = new List<SceneNode>();
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static IEnumerable<OutlinerEntry> WalkNode(
        SceneNode node,
        int depth,
        bool inheritedVisible,
        HashSet<Guid> openDefinitions)
    {
        bool visible = inheritedVisible && SceneVisibility.IsSelfVisible(node);
        yield return new OutlinerEntry(node, depth, visible);

        if (node is ComponentInstanceNode instance)
        {
            if (!openDefinitions.Add(instance.Definition.Id))
            {
                throw new InvalidOperationException($"Циклическая ссылка на компонент «{instance.Definition.Name}» в иерархии сцены.");
            }

            foreach (OutlinerEntry entry in WalkNode(instance.Definition.Content, depth + 1, visible, openDefinitions))
            {
                yield return entry;
            }

            openDefinitions.Remove(instance.Definition.Id);
        }

        foreach (SceneNode child in node.Children)
        {
            foreach (OutlinerEntry entry in WalkNode(child, depth + 1, visible, openDefinitions))
            {
                yield return entry;
            }
        }
    }
}
