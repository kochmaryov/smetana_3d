using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ToolPalette;

/// <summary>
/// Строки палитры и диалога «Настройка панели инструментов» (макет variant_a):
/// RU/EN в модуле, общий resx не трогается. Подключаются как провайдер строк.
/// </summary>
public static class ToolPaletteStrings
{
    // Служебные кнопки группы «Вид».
    public const string ServicePrintVolume = "ToolPalette.Service.PrintVolume";
    public const string ServiceTutorial = "ToolPalette.Service.Tutorial";

    // Диалог «Настройка панели инструментов» (трек C дополняет через координатора).
    public const string DialogTitle = "ToolPalette.Dialog.Title";
    public const string DialogAllTools = "ToolPalette.Dialog.AllTools";
    public const string DialogPalette = "ToolPalette.Dialog.Palette";
    public const string DialogButtonProps = "ToolPalette.Dialog.ButtonProps";
    public const string DialogNewGroup = "ToolPalette.Dialog.NewGroup";
    public const string DialogReset = "ToolPalette.Dialog.Reset";
    public const string DialogApply = "ToolPalette.Dialog.Apply";
    public const string DialogCancel = "ToolPalette.Dialog.Cancel";
    public const string DialogShowHint = "ToolPalette.Dialog.ShowHint";
    public const string DialogShowGesture = "ToolPalette.Dialog.ShowGesture";
    public const string DialogHideCompact = "ToolPalette.Dialog.HideCompact";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (ServicePrintVolume, EditorCulture.Russian) => "Зона печати",
        (ServicePrintVolume, EditorCulture.English) => "Print volume",
        (ServiceTutorial, EditorCulture.Russian) => "Урок",
        (ServiceTutorial, EditorCulture.English) => "Tutorial",
        (DialogTitle, EditorCulture.Russian) => "Настройка панели инструментов",
        (DialogTitle, EditorCulture.English) => "Tool palette settings",
        (DialogAllTools, EditorCulture.Russian) => "Все инструменты",
        (DialogAllTools, EditorCulture.English) => "All tools",
        (DialogPalette, EditorCulture.Russian) => "Панель (порядок и группы)",
        (DialogPalette, EditorCulture.English) => "Palette (order and groups)",
        (DialogButtonProps, EditorCulture.Russian) => "Свойства кнопки",
        (DialogButtonProps, EditorCulture.English) => "Button properties",
        (DialogNewGroup, EditorCulture.Russian) => "Новая группа…",
        (DialogNewGroup, EditorCulture.English) => "New group…",
        (DialogReset, EditorCulture.Russian) => "Сбросить к исходной",
        (DialogReset, EditorCulture.English) => "Reset to defaults",
        (DialogApply, EditorCulture.Russian) => "Применить",
        (DialogApply, EditorCulture.English) => "Apply",
        (DialogCancel, EditorCulture.Russian) => "Отмена",
        (DialogCancel, EditorCulture.English) => "Cancel",
        (DialogShowHint, EditorCulture.Russian) => "Показывать подсказку новичку",
        (DialogShowHint, EditorCulture.English) => "Show novice hint",
        (DialogShowGesture, EditorCulture.Russian) => "Показывать горячую клавишу",
        (DialogShowGesture, EditorCulture.English) => "Show shortcut",
        (DialogHideCompact, EditorCulture.Russian) => "Скрывать в компактном режиме",
        (DialogHideCompact, EditorCulture.English) => "Hide in compact mode",
        _ => key,
    };
}
