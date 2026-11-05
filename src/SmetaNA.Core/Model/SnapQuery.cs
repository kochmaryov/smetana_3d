using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Model;

/// <summary>
/// Поиск пользовательских точек сборки (Snap) и связей между ними: пара snap'ов
/// соединена, когда кадры совпадают «лицевая к лицевой» — позиции совпадают,
/// нормали противоположны, up совпадает (документированное поведение SketchUp).
/// </summary>
public static class SnapQuery
{
    /// <summary>Допуск сравнения позиций и направлений (мировые единицы).</summary>
    public const double ConnectionTolerance = 1e-6;

    /// <summary>
    /// Все snap-узлы сцены; скрытые поддеревья пропускаются без
    /// <paramref name="includeHidden"/>. Содержимое определений компонентов
    /// обходится один раз — возвращаются общие узлы определения.
    /// </summary>
    public static IReadOnlyList<SnapNode> FindInScene(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var result = new List<SnapNode>();
        Collect(scene.Root, includeHidden, [], result);
        return result;
    }

    /// <summary>Snap-узлы внутри поддерева (для контекста группы/компонента).</summary>
    public static IReadOnlyList<SnapNode> FindIn(SceneNode root, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(root);

        var result = new List<SnapNode>();
        Collect(root, includeHidden, [], result);
        return result;
    }

    /// <summary>
    /// Соединены ли два snap'а «лицевая к лицевой»: позиции совпадают, нормали
    /// противоположны, up совпадает.
    /// </summary>
    public static bool AreConnected(SnapFrame first, SnapFrame second, double tolerance = ConnectionTolerance)
    {
        if ((first.Position - second.Position).Length() > tolerance)
        {
            return false;
        }

        SnapFrame a = first.Normalized();
        SnapFrame b = second.Normalized();
        return (a.Direction + b.Direction).Length() <= tolerance
            && (a.Up - b.Up).Length() <= tolerance;
    }

    private static void Collect(SceneNode node, bool includeHidden, HashSet<SceneNode> visited, List<SnapNode> result)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        if (!visited.Add(node))
        {
            return;
        }

        if (node is SnapNode snap)
        {
            result.Add(snap);
        }

        if (node is ComponentInstanceNode instance)
        {
            Collect(instance.Definition.Content, includeHidden, visited, result);
        }

        foreach (SceneNode child in node.Children)
        {
            Collect(child, includeHidden, visited, result);
        }
    }
}
