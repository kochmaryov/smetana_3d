using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Новые строки диалога «Настройка панели инструментов», которых ещё нет в
/// замороженных <see cref="ToolPaletteStrings"/> (трек C не правит строки волны 0
/// — список передан координатору для переноса в <c>ToolPaletteStrings</c>).
/// Ключи сразу в пространстве имён <c>ToolPalette.Dialog.*</c>, чтобы перенос был
/// переименованием констант без смены формата файлов настроек и ресурсов.
/// </summary>
public static class ToolLayoutDialogStrings
{
    public const string DialogSubtitle = "ToolPalette.Dialog.Subtitle";
    public const string DialogSearchHint = "ToolPalette.Dialog.SearchHint";
    public const string DialogDragHint = "ToolPalette.Dialog.DragHint";
    public const string DialogRenameGroup = "ToolPalette.Dialog.RenameGroup";
    public const string DialogDeleteGroup = "ToolPalette.Dialog.DeleteGroup";
    public const string DialogMoveUp = "ToolPalette.Dialog.MoveUp";
    public const string DialogMoveDown = "ToolPalette.Dialog.MoveDown";
    public const string DialogGroupVisible = "ToolPalette.Dialog.GroupVisible";
    public const string DialogNewGroupTitle = "ToolPalette.Dialog.NewGroupTitle";
    public const string DialogRenameGroupTitle = "ToolPalette.Dialog.RenameGroupTitle";
    public const string DialogGroupNameLabel = "ToolPalette.Dialog.GroupNameLabel";
    public const string DialogGroupNameDefault = "ToolPalette.Dialog.GroupNameDefault";
    public const string DialogOk = "ToolPalette.Dialog.Ok";
    public const string DialogPropertyGroup = "ToolPalette.Dialog.PropertyGroup";
    public const string DialogPropertyGesture = "ToolPalette.Dialog.PropertyGesture";
    public const string DialogPropertyVcb = "ToolPalette.Dialog.PropertyVcb";
    public const string DialogPropertyNone = "ToolPalette.Dialog.PropertyNone";
    public const string DialogIconHint = "ToolPalette.Dialog.IconHint";
    public const string DialogClose = "ToolPalette.Dialog.Close";
    public const string DialogArrowRight = "ToolPalette.Dialog.ArrowRight";
    public const string DialogArrowLeft = "ToolPalette.Dialog.ArrowLeft";
    public const string DialogGlyphUp = "ToolPalette.Dialog.GlyphUp";
    public const string DialogGlyphDown = "ToolPalette.Dialog.GlyphDown";
    public const string DialogGrip = "ToolPalette.Dialog.Grip";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (DialogSubtitle, EditorCulture.Russian) => "Отметьте инструменты, которые видны на панели; порядок панели = порядок кнопок",
        (DialogSubtitle, EditorCulture.English) => "Check the tools shown on the palette; palette order = button order",
        (DialogSearchHint, EditorCulture.Russian) => "Ищите по имени или горячей клавише",
        (DialogSearchHint, EditorCulture.English) => "Search by name or shortcut",
        (DialogDragHint, EditorCulture.Russian) => "Перетащите за «⠿», чтобы изменить порядок",
        (DialogDragHint, EditorCulture.English) => "Drag the «⠿» handle to change the order",
        (DialogRenameGroup, EditorCulture.Russian) => "Переименовать…",
        (DialogRenameGroup, EditorCulture.English) => "Rename…",
        (DialogDeleteGroup, EditorCulture.Russian) => "Удалить группу",
        (DialogDeleteGroup, EditorCulture.English) => "Delete group",
        (DialogMoveUp, EditorCulture.Russian) => "Поднять",
        (DialogMoveUp, EditorCulture.English) => "Move up",
        (DialogMoveDown, EditorCulture.Russian) => "Опустить",
        (DialogMoveDown, EditorCulture.English) => "Move down",
        (DialogGroupVisible, EditorCulture.Russian) => "Группа видима на панели",
        (DialogGroupVisible, EditorCulture.English) => "Group is visible on the palette",
        (DialogNewGroupTitle, EditorCulture.Russian) => "Новая группа",
        (DialogNewGroupTitle, EditorCulture.English) => "New group",
        (DialogRenameGroupTitle, EditorCulture.Russian) => "Переименование группы",
        (DialogRenameGroupTitle, EditorCulture.English) => "Rename group",
        (DialogGroupNameLabel, EditorCulture.Russian) => "Имя группы",
        (DialogGroupNameLabel, EditorCulture.English) => "Group name",
        (DialogGroupNameDefault, EditorCulture.Russian) => "Мои инструменты",
        (DialogGroupNameDefault, EditorCulture.English) => "My tools",
        (DialogOk, EditorCulture.Russian) => "ОК",
        (DialogOk, EditorCulture.English) => "OK",
        (DialogPropertyGroup, EditorCulture.Russian) => "Группа",
        (DialogPropertyGroup, EditorCulture.English) => "Group",
        (DialogPropertyGesture, EditorCulture.Russian) => "Горячая клавиша",
        (DialogPropertyGesture, EditorCulture.English) => "Shortcut",
        (DialogPropertyVcb, EditorCulture.Russian) => "Ввод в VCB",
        (DialogPropertyVcb, EditorCulture.English) => "VCB input",
        (DialogPropertyNone, EditorCulture.Russian) => "—",
        (DialogPropertyNone, EditorCulture.English) => "—",
        (DialogIconHint, EditorCulture.Russian) => "Иконка: набор из 24 глифов + загрузка SVG (v1.x).",
        (DialogIconHint, EditorCulture.English) => "Icon: a set of 24 glyphs + SVG upload (v1.x).",
        (DialogClose, EditorCulture.Russian) => "✕",
        (DialogClose, EditorCulture.English) => "✕",
        (DialogArrowRight, EditorCulture.Russian) => "→",
        (DialogArrowRight, EditorCulture.English) => "→",
        (DialogArrowLeft, EditorCulture.Russian) => "←",
        (DialogArrowLeft, EditorCulture.English) => "←",
        (DialogGlyphUp, EditorCulture.Russian) => "↑",
        (DialogGlyphUp, EditorCulture.English) => "↑",
        (DialogGlyphDown, EditorCulture.Russian) => "↓",
        (DialogGlyphDown, EditorCulture.English) => "↓",
        (DialogGrip, EditorCulture.Russian) => "⠿",
        (DialogGrip, EditorCulture.English) => "⠿",
        _ => key,
    };

    /// <summary>
    /// Локализованная строка: сначала общий каталог/модули волны 0, затем новые
    /// ключи трека C. Так диалог работает и до переноса строк координатором.
    /// </summary>
    public static string Resolve(ILocalizationService localization, string key)
    {
        ArgumentNullException.ThrowIfNull(localization);
        string value = localization.Get(key);
        return value == key ? Get(key, localization.Culture) : value;
    }

    /// <summary>Счётчик кнопок группы: «5 кнопок» / «5 buttons» (русская множественность).</summary>
    public static string FormatToolCount(int count, EditorCulture culture) =>
        Format(count, culture, "кнопка", "кнопки", "кнопок", "button", "buttons");

    /// <summary>Счётчик под-инструментов в вылете: «2 в вылете» / «2 in flyout».</summary>
    public static string FormatFlyoutCount(int count, EditorCulture culture) =>
        Format(count, culture, "в вылете", "в вылете", "в вылете", "in flyout", "in flyout");

    private static string Format(
        int count,
        EditorCulture culture,
        string one,
        string few,
        string many,
        string englishOne,
        string englishMany)
    {
        if (culture == EditorCulture.English)
        {
            return count == 1 ? $"1 {englishOne}" : $"{count} {englishMany}";
        }

        int mod10 = count % 10;
        int mod100 = count % 100;
        string suffix = mod10 == 1 && mod100 != 11
            ? one
            : mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14
                ? few
                : many;
        return $"{count} {suffix}";
    }
}
