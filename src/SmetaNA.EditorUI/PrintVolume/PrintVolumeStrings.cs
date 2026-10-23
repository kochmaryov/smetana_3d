using System.Globalization;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.PrintVolume;

/// <summary>
/// Строки модуля зоны печати (задача 4.5): RU/EN держатся в своём модуле,
/// как у <see cref="Tools.Drawing.DrawingToolStrings"/> — общие resx не меняются.
/// </summary>
public static class PrintVolumeStrings
{
    /// <summary>Заголовок панели.</summary>
    public const string Title = "PrintVolume.Title";

    /// <summary>Подпись выбора профиля.</summary>
    public const string ProfileLabel = "PrintVolume.Profile";

    /// <summary>Переключатель показа зоны.</summary>
    public const string ShowVolume = "PrintVolume.Show";

    /// <summary>Габариты зоны печати.</summary>
    public const string VolumeLabel = "PrintVolume.Volume";

    /// <summary>Размер сопла.</summary>
    public const string NozzleLabel = "PrintVolume.Nozzle";

    /// <summary>Высота слоя.</summary>
    public const string LayerLabel = "PrintVolume.Layer";

    /// <summary>Примечание профиля.</summary>
    public const string NotesLabel = "PrintVolume.Notes";

    /// <summary>Единица измерения «мм».</summary>
    public const string MillimeterUnit = "PrintVolume.Unit.Mm";

    /// <summary>Строка модуля по ключу и языку; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture) => (key, culture) switch
    {
        (Title, EditorCulture.Russian) => "Зона печати",
        (Title, EditorCulture.English) => "Print Volume",
        (ProfileLabel, EditorCulture.Russian) => "Принтер",
        (ProfileLabel, EditorCulture.English) => "Printer",
        (ShowVolume, EditorCulture.Russian) => "Показывать зону",
        (ShowVolume, EditorCulture.English) => "Show volume",
        (VolumeLabel, EditorCulture.Russian) => "Габариты зоны",
        (VolumeLabel, EditorCulture.English) => "Build volume",
        (NozzleLabel, EditorCulture.Russian) => "Сопло",
        (NozzleLabel, EditorCulture.English) => "Nozzle",
        (LayerLabel, EditorCulture.Russian) => "Высота слоя",
        (LayerLabel, EditorCulture.English) => "Layer height",
        (NotesLabel, EditorCulture.Russian) => "Примечание",
        (NotesLabel, EditorCulture.English) => "Notes",
        (MillimeterUnit, EditorCulture.Russian) => "мм",
        (MillimeterUnit, EditorCulture.English) => "mm",
        _ => key,
    };

    /// <summary>Формат числа по культуре языка (запятая в RU, точка в EN).</summary>
    public static string FormatNumber(double value, EditorCulture culture) =>
        value.ToString("0.###", CultureInfo.GetCultureInfo(
            culture == EditorCulture.Russian ? "ru-RU" : "en-US"));
}
