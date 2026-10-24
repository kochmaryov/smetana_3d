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

    private static readonly List<Func<string, EditorCulture, string?>> ModuleProviders = [];

    static LocalizationService()
    {
        RegisterModuleStrings(SmetaNA.EditorUI.Tools.M6.M6ToolStrings.Get);
        RegisterModuleStrings(SmetaNA.EditorUI.ToolPalette.ToolGroupStrings.Get);
        RegisterModuleStrings(SmetaNA.EditorUI.ToolPalette.ToolPaletteStrings.Get);
    }

    /// <summary>
    /// Подключить строки модуля (например, <c>M6ToolStrings.Get</c>): используются,
    /// когда ключа нет в общих ресурсах. Возвращает отписку.
    /// </summary>
    public static IDisposable RegisterModuleStrings(Func<string, EditorCulture, string?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ModuleProviders.Add(provider);
        return new ProviderRegistration(() => ModuleProviders.Remove(provider));
    }

    public EditorCulture Culture { get; private set; } = EditorCulture.Russian;

    public event Action? CultureChanged;

    public string Get(string key) => Get(key, Culture);

    public string Get(string key, EditorCulture culture)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string? value = Resources.GetString(key, CultureInfo.GetCultureInfo(culture.ToCultureName()));
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        foreach (Func<string, EditorCulture, string?> provider in ModuleProviders)
        {
            string? moduleValue = provider(key, culture);
            if (!string.IsNullOrEmpty(moduleValue) && moduleValue != key)
            {
                return moduleValue;
            }
        }

        return key;
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

    private sealed class ProviderRegistration(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
