using System.Globalization;
using System.Resources;

namespace SmetaNA.EditorUI.Localization;

/// <summary>
/// Локализация поверх ресурсов <c>Resources/Strings*.resx</c>:
/// нейтральный русский, спутниковые сборки для английского.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager Resources =
        new("SmetaNA.EditorUI.Resources.Strings", typeof(LocalizationService).Assembly);

    public EditorCulture Culture { get; private set; } = EditorCulture.Russian;

    public event Action? CultureChanged;

    public string Get(string key) => Get(key, Culture);

    public string Get(string key, EditorCulture culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string? value = Resources.GetString(key, CultureInfo.GetCultureInfo(culture.ToCultureName()));
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public void SetCulture(EditorCulture culture)
    {
        if (Culture == culture)
        {
            return;
        }

        Culture = culture;
        CultureChanged?.Invoke();
    }
}
