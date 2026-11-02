using System.Collections.ObjectModel;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.CommandPalette;

/// <summary>Строка результата палитры команд: имя, жест, запуск.</summary>
public sealed class CommandPaletteItemViewModel
{
    private readonly Action<string> _run;

    public CommandPaletteItemViewModel(
        string commandId,
        string name,
        string gesture,
        Action<string> run)
    {
        CommandId = commandId;
        Name = name;
        Gesture = gesture;
        _run = run;
    }

    public string CommandId { get; }

    public string Name { get; }

    public string Gesture { get; }

    /// <summary>Запустить команду (инструмент или действие).</summary>
    public void Run() => _run(CommandId);
}

/// <summary>
/// Палитра команд (Ctrl+K, спека ui-redesign T4): поиск по имени среди всех
/// команд каталога жестов, запуск выбранной ≤ 2 действий.
/// </summary>
public sealed class CommandPaletteViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly ShortcutMap _shortcuts;
    private readonly Action<string> _run;
    private string _query = string.Empty;
    private CommandPaletteItemViewModel? _selectedItem;

    public CommandPaletteViewModel(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        Action<string> run)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(run);
        _localization = localization;
        _shortcuts = shortcuts;
        _run = run;
        Filter();
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>Строки результата поиска.</summary>
    public ObservableCollection<CommandPaletteItemViewModel> Results { get; } = [];

    /// <summary>Выбранная строка (Enter запускает её).</summary>
    public CommandPaletteItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    /// <summary>Строка запроса: фильтр по имени команды (без учёта регистра).</summary>
    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value))
            {
                Filter();
            }
        }
    }

    /// <summary>Заголовок окна палитры (локализация RU/EN).</summary>
    public string Title => CommandPaletteStrings.Get(
        CommandPaletteStrings.Title,
        _localization.Culture);

    /// <summary>Подсказка поля поиска (локализация RU/EN).</summary>
    public string Placeholder => CommandPaletteStrings.Get(
        CommandPaletteStrings.Placeholder,
        _localization.Culture);

    /// <summary>Запустить выбранную команду; false — если ничего не выбрано.</summary>
    public bool RunSelected()
    {
        if (SelectedItem is not { } item)
        {
            return false;
        }

        item.Run();
        return true;
    }

    private void Filter()
    {
        Results.Clear();
        string query = Query;
        foreach (string commandId in _shortcuts.CommandIds)
        {
            string name = _localization.Get(_shortcuts.GetNameKey(commandId) ?? commandId);
            if (query.Length > 0 && !name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string gesture = _shortcuts.Find(commandId)?.ToString() ?? string.Empty;
            Results.Add(new CommandPaletteItemViewModel(commandId, name, gesture, _run));
        }

        SelectedItem = Results.Count > 0 ? Results[0] : null;
    }

    private void OnCultureChanged()
    {
        // Имена команд зависят от культуры — пересобрать результаты.
        Filter();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Placeholder));
    }
}
