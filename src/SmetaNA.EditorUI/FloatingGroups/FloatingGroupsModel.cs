namespace SmetaNA.EditorUI.FloatingGroups;

/// <summary>Состояние размещения группы инструментов (спека ui-redesign T3).</summary>
public enum FloatingGroupState
{
    /// <summary>Группа в левой палитре (по умолчанию).</summary>
    Palette,

    /// <summary>Плавающее окно группы — в любом месте экрана.</summary>
    Floating,

    /// <summary>Прикреплена колонкой к левому краю рабочей области.</summary>
    Left,

    /// <summary>Прикреплена колонкой к правому краю рабочей области.</summary>
    Right,

    /// <summary>Прикреплена строкой к верхнему краю рабочей области.</summary>
    Top,

    /// <summary>Прикреплена строкой к нижнему краю рабочей области.</summary>
    Bottom,
}

/// <summary>Размещение одной группы: состояние и координаты плавающего окна.</summary>
public sealed record FloatingGroupPlacement(string GroupId, FloatingGroupState State, double X, double Y);

/// <summary>
/// Модель размещения перемещаемых групп: версия схемы и миграция —
/// как у раскладки палитры <c>tool-layout.json</c> (T3, риск R7).
/// </summary>
public sealed record FloatingGroupsModel(int Version, IReadOnlyList<FloatingGroupPlacement> Groups)
{
    public const int CurrentVersion = 1;

    /// <summary>Дефолт: все группы в палитре.</summary>
    public static FloatingGroupsModel CreateDefault() => new(CurrentVersion, []);
}

/// <summary>
/// Миграция размещений к текущей схеме: неизвестные группы отбрасываются
/// (удалены из реестра), новые добавляются состоянием «в палитре».
/// </summary>
public static class FloatingGroupsMigrations
{
    public static FloatingGroupsModel UpgradeToCurrent(
        FloatingGroupsModel model,
        IReadOnlyList<string> knownGroupIds)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(knownGroupIds);

        Dictionary<string, FloatingGroupPlacement> kept = model.Groups
            .Where(placement => knownGroupIds.Contains(placement.GroupId))
            .GroupBy(placement => placement.GroupId)
            .ToDictionary(group => group.Key, group => group.First());

        List<FloatingGroupPlacement> result = [];
        foreach (string groupId in knownGroupIds)
        {
            result.Add(kept.TryGetValue(groupId, out FloatingGroupPlacement? placement)
                ? placement
                : new FloatingGroupPlacement(groupId, FloatingGroupState.Palette, 0, 0));
        }

        return new FloatingGroupsModel(FloatingGroupsModel.CurrentVersion, result);
    }
}
