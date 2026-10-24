using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Применение раскладки к группам палитры без перезапуска (T3): из
/// <see cref="ToolLayoutModel"/> собираются видимые группы и кнопки в
/// пользовательском порядке. Под-инструменты вылетов остаются при своей кнопке
/// (<see cref="PaletteButton.FlyoutItems"/>) и следуют за ней при скрытии и
/// перестановке. Источник кнопок — <see cref="ToolGroupRegistry"/> (риск R6).
/// </summary>
public static class ToolLayoutPaletteResolver
{
    /// <summary>
    /// Группы палитры по раскладке: только видимые группы с видимыми кнопками,
    /// в порядке раскладки; пустые группы не показываются.
    /// </summary>
    public static IReadOnlyList<PaletteGroup> Resolve(ToolLayoutModel layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Dictionary<string, PaletteButton> buttons = BuildTopLevelIndex();
        HashSet<string> usedCommands = new(StringComparer.Ordinal);
        List<PaletteGroup> result = [];

        foreach (ToolLayoutGroup group in layout.Groups)
        {
            if (!group.Visible)
            {
                continue;
            }

            List<PaletteButton> groupButtons = [];
            foreach (ToolLayoutItem item in group.Items.OrderBy(item => item.Order))
            {
                if (!item.Visible || !buttons.TryGetValue(item.CommandId, out PaletteButton? button))
                {
                    continue;
                }

                if (!usedCommands.Add(button.Definition.CommandId))
                {
                    continue;
                }

                groupButtons.Add(button);
            }

            if (groupButtons.Count == 0)
            {
                continue;
            }

            result.Add(new PaletteGroup(group.Id, GroupName(group), groupButtons));
        }

        return result;
    }

    /// <summary>
    /// Кнопка палитры по команде: под-инструмент вылета сводится к своей кнопке;
    /// <c>null</c> — команда неизвестна реестру.
    /// </summary>
    public static PaletteButton? ResolveButton(string commandId)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        return BuildTopLevelIndex().GetValueOrDefault(commandId);
    }

    private static string GroupName(ToolLayoutGroup group) =>
        group.CustomName
            ?? ToolGroupRegistry.DefaultGroups.FirstOrDefault(candidate => candidate.Id == group.Id)?.NameKey
            ?? group.Id;

    private static Dictionary<string, PaletteButton> BuildTopLevelIndex()
    {
        Dictionary<string, PaletteButton> index = new(StringComparer.Ordinal);
        foreach (PaletteGroup group in ToolGroupRegistry.DefaultGroups)
        {
            foreach (PaletteButton button in group.Buttons)
            {
                index.TryAdd(button.Definition.CommandId, button);
                foreach (PaletteButton flyout in button.FlyoutItems)
                {
                    index.TryAdd(flyout.Definition.CommandId, button);
                }
            }
        }

        return index;
    }
}
