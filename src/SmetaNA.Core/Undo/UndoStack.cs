namespace SmetaNA.Core.Undo;

/// <summary>Стек undo/redo: каркас истории редактирования (задача 0.5).</summary>
public sealed class UndoStack
{
    private readonly List<IEditCommand> _undo = [];
    private readonly List<IEditCommand> _redo = [];
    private readonly int _maxDepth;

    public UndoStack(int maxDepth = 100)
    {
        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Глубина истории должна быть положительной.");
        }

        _maxDepth = maxDepth;
    }

    /// <summary>Срабатывает при любом изменении стека — для обновления UI.</summary>
    public event Action? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? NextUndoDescription => CanUndo ? _undo[^1].Description : null;
    public string? NextRedoDescription => CanRedo ? _redo[^1].Description : null;

    public void Push(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Add(command);
        if (_undo.Count > _maxDepth)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("История пуста: отменять нечего.");
        }

        IEditCommand command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo();
        _redo.Add(command);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Повторять нечего.");
        }

        IEditCommand command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Execute();
        _undo.Add(command);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}
