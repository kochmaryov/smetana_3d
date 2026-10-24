using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>Элемент раскладки: инструмент или служебная команда в группе палитры.</summary>
/// <param name="CommandId">Идентификатор команды (совпадает с ToolDefinition.CommandId).</param>
/// <param name="Visible">Кнопка видима в палитре.</param>
/// <param name="Order">Порядок внутри группы (0..).</param>
public sealed record ToolLayoutItem(string CommandId, bool Visible, int Order);

/// <summary>Группа раскладки: системная (<c>CustomName</c> == null) или пользовательская.</summary>
/// <param name="Id">Стабильный идентификатор группы.</param>
/// <param name="CustomName">Имя пользовательской группы; null — имя из реестра групп.</param>
/// <param name="Visible">Группа видима в палитре.</param>
/// <param name="Items">Кнопки группы (под-инструменты вылетов в раскладку не входят).</param>
public sealed record ToolLayoutGroup(
    string Id,
    string? CustomName,
    bool Visible,
    IReadOnlyList<ToolLayoutItem> Items);

/// <summary>
/// Раскладка палитры инструментов, schema v1 (спека tool-palette §4 «Хранение
/// раскладки»): группы, порядок и видимость кнопок. Пользовательская раскладка
/// хранится в JSON (<see cref="IToolLayoutStore"/>); при добавлении новых
/// инструментов они попадают в конец своей группы дефолтной раскладки (риск R4 —
/// миграция через <see cref="CurrentVersion"/>).
/// </summary>
/// <param name="Version">Версия схемы раскладки.</param>
/// <param name="Groups">Группы палитры в порядке отображения.</param>
public sealed record ToolLayoutModel(
    int Version,
    IReadOnlyList<ToolLayoutGroup> Groups)
{
    /// <summary>Текущая версия схемы.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Дефолтная раскладка по реестру групп (все кнопки видимы, порядок — как в реестре).</summary>
    public static ToolLayoutModel CreateDefault() =>
        CreateDefault(ToolGroupRegistry.DefaultGroups);

    /// <summary>Дефолтная раскладка по переданным группам (для тестов и миграции).</summary>
    public static ToolLayoutModel CreateDefault(IReadOnlyList<PaletteGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return new ToolLayoutModel(
            CurrentVersion,
            [.. groups.Select(group => new ToolLayoutGroup(
                group.Id,
                CustomName: null,
                Visible: true,
                Items: [.. group.Buttons.Select((button, index) =>
                    new ToolLayoutItem(button.Definition.CommandId, Visible: true, Order: index))]))]);
    }
}
