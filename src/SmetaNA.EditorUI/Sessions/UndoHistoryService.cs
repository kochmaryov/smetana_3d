using SmetaNA.Core.Undo;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>Запись истории для панели истории.</summary>
/// <param name="Description">Описание команды.</param>
/// <param name="IsApplied">Применена ли команда к текущему состоянию.</param>
public sealed record UndoHistoryEntry(string Description, bool IsApplied);

/// <summary>
/// История редактирования для UI: работает с замороженным контрактом
/// <see cref="UndoStack"/> из SmetaNA.Core (только чтение — поэтому список
/// записей для панели ведётся здесь параллельно со стеком).
/// </summary>
public sealed class UndoHistoryService
{
    private readonly List<string> _log = [];
    private int _cursor;

    public UndoHistoryService(UndoStack stack, int maxDepth = 100)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Глубина истории должна быть положительной.");
        }

        Stack = stack;
        MaxDepth = maxDepth;
    }

    /// <summary>Стек Core: кнопки undo/redo и история привязаны к нему.</summary>
    public UndoStack Stack { get; }

    public int MaxDepth { get; }

    /// <summary>Срабатывает при любом изменении истории.</summary>
    public event Action? Changed;

    public bool CanUndo => Stack.CanUndo;
    public bool CanRedo => Stack.CanRedo;
    public string? NextUndoDescription => Stack.NextUndoDescription;
    public string? NextRedoDescription => Stack.NextRedoDescription;

    /// <summary>Записи истории; первые <see cref="EntriesAppliedCount"/> применены.</summary>
    public IReadOnlyList<UndoHistoryEntry> Entries
    {
        get
        {
            var entries = new List<UndoHistoryEntry>(_log.Count);
            for (int i = 0; i < _log.Count; i++)
            {
                entries.Add(new UndoHistoryEntry(_log[i], i < _cursor));
            }

            return entries;
        }
    }

    /// <summary>Индекс текущего состояния (число применённых команд).</summary>
    public int EntriesAppliedCount => _cursor;

    /// <summary>Выполнить команду (команда выполняется внутри стека) и записать в историю.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Stack.Push(command);

        if (_cursor < _log.Count)
        {
            _log.RemoveRange(_cursor, _log.Count - _cursor);
        }

        _log.Add(command.Description);
        _cursor++;
        TrimToMaxDepth();
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        Stack.Undo();
        _cursor--;
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        Stack.Redo();
        _cursor++;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Stack.Clear();
        _log.Clear();
        _cursor = 0;
        Changed?.Invoke();
    }

    private void TrimToMaxDepth()
    {
        if (_log.Count <= MaxDepth)
        {
            return;
        }

        int overflow = _log.Count - MaxDepth;
        _log.RemoveRange(0, overflow);
        _cursor = Math.Max(0, _cursor - overflow);
    }
}
