namespace SmetaNA.EditorUI.Localization;

/// <summary>Поддерживаемые языки интерфейса (DoD §5.7: 100% строк RU/EN).</summary>
public enum EditorCulture
{
    Russian,
    English,
}

public static class EditorCultureExtensions
{
    /// <summary>Имя культуры .NET для ресурсов локализации.</summary>
    public static string ToCultureName(this EditorCulture culture) => culture switch
    {
        EditorCulture.Russian => "ru",
        EditorCulture.English => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(culture), culture, null),
    };
}
