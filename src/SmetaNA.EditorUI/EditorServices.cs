using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Themes;

namespace SmetaNA.EditorUI;

/// <summary>Состав приложения: сервисы, общие для окна и элементов управления.</summary>
public static class EditorServices
{
    public static ILocalizationService Localization { get; } = new LocalizationService();

    public static ThemeService Theme { get; } = new();
}
