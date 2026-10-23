namespace SmetaNA.EditorUI.Commands;

/// <summary>Описание команды с маппингом горячих клавиш.</summary>
public sealed record ShortcutDefinition(string CommandId, string NameKey, Shortcut Gesture);

/// <summary>
/// Идентификаторы команд редактора и их маппинги по умолчанию (заготовка —
/// инструменты подключаются в фазе 2).
/// </summary>
public static class ShortcutCatalog
{
    public const string ProjectNew = "project.new";
    public const string EditUndo = "edit.undo";
    public const string EditRedo = "edit.redo";
    public const string ToolSelect = "tool.select";
    public const string ToolLine = "tool.line";
    public const string ToolRectangle = "tool.rectangle";
    public const string ToolCircle = "tool.circle";
    public const string ToolPolygon = "tool.polygon";
    public const string ToolArc = "tool.arc";
    public const string ToolFreehand = "tool.freehand";
    public const string ToolPushPull = "tool.pushpull";
    public const string ToolOffset = "tool.offset";
    public const string ToolMove = "tool.move";
    public const string ToolRotate = "tool.rotate";
    public const string ToolScale = "tool.scale";
    public const string ToolEraser = "tool.eraser";
    public const string ToolSoften = "tool.soften";
    public const string ToolPaintBucket = "tool.paintbucket";
    public const string ViewToggleTheme = "view.toggleTheme";
    public const string ViewToggleLanguage = "view.toggleLanguage";

    /// <summary>Маппинги по умолчанию; порядок — порядок отображения в списке.</summary>
    public static IReadOnlyList<ShortcutDefinition> Defaults { get; } =
    [
        new(ProjectNew, Localization.LocalizationKeys.ProjectNew, Shortcut.Parse("Ctrl+N")),
        new(EditUndo, Localization.LocalizationKeys.EditUndo, Shortcut.Parse("Ctrl+Z")),
        new(EditRedo, Localization.LocalizationKeys.EditRedo, Shortcut.Parse("Ctrl+Y")),
        new(ToolSelect, Localization.LocalizationKeys.ToolSelect, Shortcut.Parse("Space")),
        new(ToolLine, Localization.LocalizationKeys.ToolLine, Shortcut.Parse("L")),
        new(ToolRectangle, Localization.LocalizationKeys.ToolRectangle, Shortcut.Parse("R")),
        new(ToolCircle, Localization.LocalizationKeys.ToolCircle, Shortcut.Parse("C")),
        new(ToolPolygon, Localization.LocalizationKeys.ToolPolygon, Shortcut.Parse("G")),
        new(ToolArc, Localization.LocalizationKeys.ToolArc, Shortcut.Parse("A")),
        new(ToolFreehand, Localization.LocalizationKeys.ToolFreehand, Shortcut.Parse("F")),
        new(ToolPushPull, Localization.LocalizationKeys.ToolPushPull, Shortcut.Parse("P")),
        new(ToolOffset, Localization.LocalizationKeys.ToolOffset, Shortcut.Parse("O")),
        new(ToolMove, Localization.LocalizationKeys.ToolMove, Shortcut.Parse("M")),
        new(ToolRotate, Localization.LocalizationKeys.ToolRotate, Shortcut.Parse("Q")),
        new(ToolScale, Localization.LocalizationKeys.ToolScale, Shortcut.Parse("S")),
        new(ToolEraser, Localization.LocalizationKeys.ToolEraser, Shortcut.Parse("E")),
        new(ToolSoften, Localization.LocalizationKeys.ToolSoften, Shortcut.Parse("H")),
        new(ToolPaintBucket, Localization.LocalizationKeys.ToolPaintBucket, Shortcut.Parse("B")),
        new(ViewToggleTheme, Localization.LocalizationKeys.CommandToggleTheme, Shortcut.Parse("Ctrl+Shift+T")),
        new(ViewToggleLanguage, Localization.LocalizationKeys.CommandToggleLanguage, Shortcut.Parse("Ctrl+Shift+L")),
    ];
}
