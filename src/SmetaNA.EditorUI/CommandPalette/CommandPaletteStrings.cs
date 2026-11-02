using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.CommandPalette;

/// <summary>
/// Строки палитры команд (Ctrl+K): RU/EN в модуле, общий resx не трогается.
/// </summary>
public static class CommandPaletteStrings
{
    public const string Title = "CommandPalette.Title";
    public const string Placeholder = "CommandPalette.Placeholder";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (Title, EditorCulture.Russian) => "Палитра команд",
        (Title, EditorCulture.English) => "Command palette",
        (Placeholder, EditorCulture.Russian) => "Поиск команды…",
        (Placeholder, EditorCulture.English) => "Search command…",
        _ => key,
    };
}
