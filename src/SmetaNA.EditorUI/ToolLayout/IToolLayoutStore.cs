namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Хранилище раскладки палитры (контракт волны 0; реализация — трек C):
/// JSON в каталоге настроек приложения, версия схемы и миграция.
/// </summary>
public interface IToolLayoutStore
{
    /// <summary>
    /// Текущая раскладка. Дефолтная — если файла нет, он повреждён или версия
    /// схемы новее поддерживаемой.
    /// </summary>
    ToolLayoutModel Load();

    /// <summary>Сохранить раскладку (перезаписывая пользовательскую).</summary>
    void Save(ToolLayoutModel layout);

    /// <summary>Сбросить к дефолтной: удалить пользовательскую раскладку и вернуть её.</summary>
    ToolLayoutModel Reset();
}
