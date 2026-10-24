using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>
/// Имена групп палитры (RU/EN держатся в модуле; общий resx не трогается —
/// конвенция вроде <c>DrawingToolStrings</c>). Подключаются как провайдер строк.
/// </summary>
public static class ToolGroupStrings
{
    public const string GroupDrawing = "ToolPalette.Group.Drawing";
    public const string GroupEditing = "ToolPalette.Group.Editing";
    public const string GroupOperations = "ToolPalette.Group.Operations";
    public const string GroupDimensions = "ToolPalette.Group.Dimensions";
    public const string GroupView = "ToolPalette.Group.View";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (GroupDrawing, EditorCulture.Russian) => "Рисование",
        (GroupDrawing, EditorCulture.English) => "Drawing",
        (GroupEditing, EditorCulture.Russian) => "Правка",
        (GroupEditing, EditorCulture.English) => "Edit",
        (GroupOperations, EditorCulture.Russian) => "Операции",
        (GroupOperations, EditorCulture.English) => "Operations",
        (GroupDimensions, EditorCulture.Russian) => "Размеры",
        (GroupDimensions, EditorCulture.English) => "Dimensions",
        (GroupView, EditorCulture.Russian) => "Вид",
        (GroupView, EditorCulture.English) => "View",
        _ => key,
    };
}
