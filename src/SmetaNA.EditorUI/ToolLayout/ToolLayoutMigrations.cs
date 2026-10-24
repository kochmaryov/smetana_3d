using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Миграция раскладки палитры между версиями схемы (риск R4) и примирение с
/// реестром групп: новые инструменты добавляются в конец своей группы, выбывшие
/// отбрасываются, дубли схлопываются (инструмент живёт в одной группе).
/// </summary>
public static class ToolLayoutMigrations
{
    /// <summary>
    /// Привести раскладку к текущей версии схемы и актуальному составу реестра.
    /// Версия результата — всегда <see cref="ToolLayoutModel.CurrentVersion"/>.
    /// </summary>
    /// <param name="layout">Загруженная раскладка (любая поддерживаемая версия).</param>
    /// <param name="registryGroups">Группы реестра (состав инструментов).</param>
    public static ToolLayoutModel UpgradeToCurrent(
        ToolLayoutModel layout,
        IReadOnlyList<PaletteGroup> registryGroups)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(registryGroups);

        Dictionary<string, PaletteButton> buttonsByCommand = BuildButtonIndex(registryGroups);
        Dictionary<string, PaletteGroup> groupsById = registryGroups.ToDictionary(group => group.Id);
        HashSet<string> usedCommands = new(StringComparer.Ordinal);
        List<ToolLayoutGroup> result = [];

        // Проход 1: явные размещения пользователя (включая свои группы) — по порядку файла.
        foreach (ToolLayoutGroup group in layout.Groups)
        {
            if (group is null || string.IsNullOrWhiteSpace(group.Id))
            {
                continue;
            }

            bool isSystem = groupsById.TryGetValue(group.Id, out PaletteGroup? _);
            List<ToolLayoutItem> items = [];
            foreach (ToolLayoutItem item in group.Items.OrderBy(item => item.Order))
            {
                if (item is null || string.IsNullOrWhiteSpace(item.CommandId))
                {
                    continue;
                }

                string commandId = TopLevelCommandId(item.CommandId, buttonsByCommand);
                if (commandId.Length == 0 || !usedCommands.Add(commandId))
                {
                    continue;
                }

                items.Add(new ToolLayoutItem(commandId, item.Visible, items.Count));
            }

            // Системные группы не переименовываются (спека §7); свои несут имя пользователя.
            result.Add(new ToolLayoutGroup(
                group.Id,
                isSystem ? null : (group.CustomName ?? group.Id),
                group.Visible,
                items));
        }

        // Проход 2: новые инструменты реестра — в конец своей группы (риск R4).
        for (int groupIndex = 0; groupIndex < result.Count; groupIndex++)
        {
            ToolLayoutGroup toolGroup = result[groupIndex];
            if (!groupsById.TryGetValue(toolGroup.Id, out PaletteGroup? registryGroup))
            {
                continue;
            }

            List<ToolLayoutItem> items = [.. toolGroup.Items];
            foreach (PaletteButton button in registryGroup.Buttons)
            {
                if (usedCommands.Add(button.Definition.CommandId))
                {
                    items.Add(new ToolLayoutItem(button.Definition.CommandId, Visible: true, items.Count));
                }
            }

            result[groupIndex] = toolGroup with { Items = items };
        }

        // Проход 3: группы реестра, которых ещё нет в раскладке, — целиком в конец.
        foreach (PaletteGroup registryGroup in registryGroups)
        {
            if (result.Any(group => group.Id == registryGroup.Id))
            {
                continue;
            }

            List<ToolLayoutItem> items = [];
            foreach (PaletteButton button in registryGroup.Buttons)
            {
                if (usedCommands.Add(button.Definition.CommandId))
                {
                    items.Add(new ToolLayoutItem(button.Definition.CommandId, Visible: true, items.Count));
                }
            }

            result.Add(new ToolLayoutGroup(registryGroup.Id, CustomName: null, Visible: true, items));
        }

        return new ToolLayoutModel(ToolLayoutModel.CurrentVersion, result);
    }

    /// <summary>
    /// Верхнеуровневая команда кнопки: под-инструмент вылета сводится к своей
    /// кнопке (под-инструменты следуют за кнопкой), неизвестная — к пустой строке.
    /// </summary>
    private static string TopLevelCommandId(string commandId, Dictionary<string, PaletteButton> buttonsByCommand)
    {
        if (buttonsByCommand.TryGetValue(commandId, out PaletteButton? button))
        {
            return button.Definition.CommandId;
        }

        return string.Empty;
    }

    private static Dictionary<string, PaletteButton> BuildButtonIndex(IReadOnlyList<PaletteGroup> registryGroups)
    {
        Dictionary<string, PaletteButton> index = new(StringComparer.Ordinal);
        foreach (PaletteGroup group in registryGroups)
        {
            foreach (PaletteButton button in group.Buttons)
            {
                index.TryAdd(button.Definition.CommandId, button);
                foreach (PaletteButton flyout in button.FlyoutItems)
                {
                    // Под-инструмент указывает на свою кнопку — порядок следования сохраняется.
                    index.TryAdd(flyout.Definition.CommandId, button);
                }
            }
        }

        return index;
    }
}
