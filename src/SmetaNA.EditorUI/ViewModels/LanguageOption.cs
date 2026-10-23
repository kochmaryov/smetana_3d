using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Пункт переключателя языка интерфейса.</summary>
public sealed class LanguageOption : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public LanguageOption(ILocalizationService localization, EditorCulture culture)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Culture = culture;
        _localization.CultureChanged += InvalidateAll;
    }

    public EditorCulture Culture { get; }

    public string Name => _localization.Get(
        Culture == EditorCulture.Russian
            ? LocalizationKeys.LanguageRussian
            : LocalizationKeys.LanguageEnglish);
}
