using System.Windows.Input;

namespace SmetaNA.EditorUI.Commands;

/// <summary>Минимальная команда MVVM без внешних зависимостей.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute)
        : this(Wrap(execute), null)
    {
    }

    private static Action<object?> Wrap(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return _ => execute();
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Пересчитать доступность команды (после изменения состояния).</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
