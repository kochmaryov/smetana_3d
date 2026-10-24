using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>Идентификаторы служебных команд палитры (не инструменты Core).</summary>
public static class ToolPaletteCommandIds
{
    /// <summary>«Зона печати» — служебная кнопка группы «Вид».</summary>
    public const string PrintVolume = "view.printVolume";

    /// <summary>«Урок» — служебная кнопка группы «Вид» (старт урока S1).</summary>
    public const string Tutorial = "help.tutorial";
}

/// <summary>
/// Кнопка палитры: инструмент и его под-инструменты в вылете
/// («Дуга» — 2 варианта, «Зеркало» — 3 варианта; макет variant_a).
/// </summary>
/// <param name="Definition">Описание команды (имя, подсказка, VCB).</param>
/// <param name="IconId">Глиф кнопки.</param>
/// <param name="FlyoutItems">Под-инструменты вылета; пусто — вылета нет.</param>
public sealed record PaletteButton(
    ToolDefinition Definition,
    Icons.ToolIconId IconId,
    IReadOnlyList<PaletteButton> FlyoutItems);

/// <summary>Группа палитры (раздел с подписью).</summary>
/// <param name="Id">Стабильный идентификатор группы (для раскладки).</param>
/// <param name="NameKey">Ключ имени в <see cref="ToolGroupStrings"/>.</param>
/// <param name="Buttons">Кнопки группы в порядке по умолчанию.</param>
public sealed record PaletteGroup(
    string Id,
    string NameKey,
    IReadOnlyList<PaletteButton> Buttons);

/// <summary>
/// Реестр групп и кнопок палитры инструментов — единый источник для палитры,
/// диалога настройки и дефолтной раскладки (спека tool-palette §4). Состав — по
/// утверждённому макету variant_a: Рисование / Правка / Операции / Размеры / Вид.
/// <para>
/// Имена и подсказки — из общего каталога <see cref="LocalizationKeys"/> (кроме
/// M6 — их модульные ключи резолвятся провайдером строк). VCB-виды совпадают с
/// текущей лентой <c>MainWindowViewModel</c>; расхождение с
/// <see cref="Tools.Editing.RegisterEditingTools"/> (Move=Length, Scale=Radius)
/// осознанное: источник истины — лента, приведение к единому виду — волна 2.
/// </para>
/// </summary>
public static class ToolGroupRegistry
{
    public const string DrawingGroupId = "drawing";
    public const string EditingGroupId = "editing";
    public const string OperationsGroupId = "operations";
    public const string DimensionsGroupId = "dimensions";
    public const string ViewGroupId = "view";

    /// <summary>Группы и кнопки по умолчанию (порядок — порядок палитры).</summary>
    public static IReadOnlyList<PaletteGroup> DefaultGroups { get; } =
    [
        new(DrawingGroupId, ToolGroupStrings.GroupDrawing,
        [
            Button(ShortcutCatalog.ToolSelect, LocalizationKeys.ToolSelect, null, LocalizationKeys.ToolHintSelect),
            Button(ShortcutCatalog.ToolLine, LocalizationKeys.ToolLine, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintLine),
            Button(ShortcutCatalog.ToolRectangle, LocalizationKeys.ToolRectangle, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintRectangle),
            Button(ShortcutCatalog.ToolCircle, LocalizationKeys.ToolCircle, Vcb.VcbInputKind.Radius, LocalizationKeys.ToolHintCircle),
            Button(ShortcutCatalog.ToolPolygon, LocalizationKeys.ToolPolygon, Vcb.VcbInputKind.Radius, LocalizationKeys.ToolHintPolygon),
            Button(
                ShortcutCatalog.ToolArc,
                LocalizationKeys.ToolArc,
                Vcb.VcbInputKind.Angle,
                LocalizationKeys.ToolHintArc,
                flyout:
                [
                    Button(ShortcutCatalog.ToolArc, LocalizationKeys.ToolArc, Vcb.VcbInputKind.Angle, LocalizationKeys.ToolHintArc),
                    Button(ShortcutCatalog.ToolArc3Point, LocalizationKeys.ToolArc3Point, Vcb.VcbInputKind.Radius, LocalizationKeys.ToolHintArc3Point),
                ]),
            Button(ShortcutCatalog.ToolFreehand, LocalizationKeys.ToolFreehand, null, LocalizationKeys.ToolHintFreehand),
        ]),
        new(EditingGroupId, ToolGroupStrings.GroupEditing,
        [
            Button(ShortcutCatalog.ToolPushPull, LocalizationKeys.ToolPushPull, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintPushPull),
            Button(ShortcutCatalog.ToolOffset, LocalizationKeys.ToolOffset, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintOffset),
            Button(ShortcutCatalog.ToolMove, LocalizationKeys.ToolMove, Vcb.VcbInputKind.Coordinate, LocalizationKeys.ToolHintMove),
            Button(ShortcutCatalog.ToolRotate, LocalizationKeys.ToolRotate, Vcb.VcbInputKind.Angle, LocalizationKeys.ToolHintRotate),
            Button(ShortcutCatalog.ToolScale, LocalizationKeys.ToolScale, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintScale),
            Button(ShortcutCatalog.ToolEraser, LocalizationKeys.ToolEraser, null, LocalizationKeys.ToolHintEraser),
            Button(ShortcutCatalog.ToolSoften, LocalizationKeys.ToolSoften, Vcb.VcbInputKind.Angle, LocalizationKeys.ToolHintSoften),
            Button(ShortcutCatalog.ToolPaintBucket, LocalizationKeys.ToolPaintBucket, null, LocalizationKeys.ToolHintPaintBucket),
        ]),
        new(OperationsGroupId, ToolGroupStrings.GroupOperations,
        [
            FromM6(ShortcutCatalog.ToolArrayLinear),
            FromM6(ShortcutCatalog.ToolArrayPolar),
            FromM6(ShortcutCatalog.ToolMirrorAxis, flyout:
            [
                FromM6(ShortcutCatalog.ToolMirrorAxis),
                FromM6(ShortcutCatalog.ToolMirrorPoints),
                FromM6(ShortcutCatalog.ToolMirrorFace),
            ]),
            FromM6(ShortcutCatalog.ToolFollowMe),
            FromM6(ShortcutCatalog.ToolRevolve),
        ]),
        new(DimensionsGroupId, ToolGroupStrings.GroupDimensions,
        [
            Button(ShortcutCatalog.ToolDimension, LocalizationKeys.ToolDimension, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintDimension),
            Button(ShortcutCatalog.ToolScaleToDimension, LocalizationKeys.ToolScaleToDimension, Vcb.VcbInputKind.Length, LocalizationKeys.ToolHintScaleToDimension),
        ]),
        new(ViewGroupId, ToolGroupStrings.GroupView,
        [
            Button(ToolPaletteCommandIds.PrintVolume, ToolPaletteStrings.ServicePrintVolume, null, null),
            Button(ToolPaletteCommandIds.Tutorial, ToolPaletteStrings.ServiceTutorial, null, null),
        ]),
    ];

    /// <summary>Все команды реестра плоским списком (включая под-инструменты вылетов).</summary>
    public static IEnumerable<ToolDefinition> AllDefinitions() =>
        DefaultGroups
            .SelectMany(group => group.Buttons)
            .SelectMany(button => button.FlyoutItems.Count > 0 ? button.FlyoutItems : [button])
            .Select(button => button.Definition)
            .DistinctBy(definition => definition.CommandId);

    /// <summary>
    /// Кнопка по идентификатору команды; <c>null</c> — не найдена. Если команда —
    /// кнопка с вылетом («Дуга», «Зеркало»), возвращается кнопка, а не под-инструмент.
    /// </summary>
    public static PaletteButton? FindButton(string commandId)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        IEnumerable<PaletteButton> buttons = DefaultGroups.SelectMany(group => group.Buttons);
        return buttons
            .Concat(buttons.SelectMany(button => button.FlyoutItems))
            .FirstOrDefault(button => button.Definition.CommandId == commandId);
    }

    /// <summary>Служебная (не tool-овая) команда палитры?</summary>
    public static bool IsServiceCommand(string commandId) =>
        commandId is ToolPaletteCommandIds.PrintVolume or ToolPaletteCommandIds.Tutorial;

    private static PaletteButton Button(
        string commandId,
        string nameKey,
        Vcb.VcbInputKind? vcbKind,
        string? hintKey,
        IReadOnlyList<PaletteButton>? flyout = null) =>
        new(
            new ToolDefinition(commandId, nameKey, vcbKind, hintKey),
            Icons.ToolIconMap.ForCommand(commandId),
            flyout ?? []);

    /// <summary>Кнопка из регистраций модуля M6 (имя/подсказка/VCB — из модуля).</summary>
    private static PaletteButton FromM6(string commandId, IReadOnlyList<PaletteButton>? flyout = null)
    {
        ToolDefinition definition = Tools.M6.RegisterM6Tools.RibbonDefinitions
            .First(candidate => candidate.CommandId == commandId);
        return new PaletteButton(
            definition,
            Icons.ToolIconMap.ForCommand(commandId),
            flyout ?? []);
    }
}
