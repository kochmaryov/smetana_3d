namespace SmetaNA.EditorUI.Localization;

/// <summary>
/// Доступ к локализованным строкам интерфейса: RU/EN без перезапуска
/// (спека §3.2 п. 6, DoD §5.7).
/// </summary>
public interface ILocalizationService
{
    /// <summary>Текущий язык интерфейса.</summary>
    EditorCulture Culture { get; }

    /// <summary>Срабатывает при смене языка — для обновления привязок.</summary>
    event Action? CultureChanged;

    /// <summary>Строка для текущего языка; неизвестный ключ возвращается как есть.</summary>
    string Get(string key);

    /// <summary>Строка для указанного языка.</summary>
    string Get(string key, EditorCulture culture);

    /// <summary>Сменить язык интерфейса.</summary>
    void SetCulture(EditorCulture culture);
}
