using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Sessions;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Синхронизация панели истории с командами инструментов (задача 6.1):
/// команды, попавшие в UndoStack через ToolContext.Execute, видны в журнале
/// с описаниями и участвуют в undo/redo.
/// </summary>
public sealed class UndoHistorySyncTests
{
    private sealed class StubCommand(string description) : IEditCommand
    {
        public string Description => description;
        public void Execute() { }
        public void Undo() { }
    }

    [Fact]
    public void ExternalPush_AppearsInJournal_WithDescription()
    {
        var stack = new UndoStack();
        var history = new UndoHistoryService(stack);

        // так команды выполняют инструменты: напрямую в стек Core
        stack.Push(new StubCommand("Выдавливание грани"));

        IReadOnlyList<UndoHistoryEntry> entries = history.Entries;
        Assert.Single(entries);
        Assert.Equal("Выдавливание грани", entries[0].Description);
        Assert.True(entries[0].IsApplied);
        Assert.Equal(1, history.EntriesAppliedCount);
    }

    [Fact]
    public void ExternalPush_AfterUndo_DropsRedoTail()
    {
        var stack = new UndoStack();
        var history = new UndoHistoryService(stack);
        stack.Push(new StubCommand("первое"));
        history.Execute(new StubCommand("второе"));
        history.Undo();

        stack.Push(new StubCommand("третье"));

        IReadOnlyList<UndoHistoryEntry> entries = history.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal("первое", entries[0].Description);
        Assert.Equal("третье", entries[1].Description);
        Assert.False(history.CanRedo);
        Assert.Equal(2, history.EntriesAppliedCount);
    }

    [Fact]
    public void MixedCommands_UndoRedo_MovesCurrentMarker()
    {
        var stack = new UndoStack();
        var history = new UndoHistoryService(stack);
        stack.Push(new StubCommand("инструмент"));
        history.Execute(new StubCommand("строка значений"));

        history.Undo();
        Assert.Equal(1, history.EntriesAppliedCount);
        Assert.Equal("инструмент", history.NextUndoDescription);

        history.Undo();
        Assert.Equal(0, history.EntriesAppliedCount);
        Assert.DoesNotContain(history.Entries, entry => entry.IsApplied);

        history.Redo();
        history.Redo();
        Assert.Equal(2, history.EntriesAppliedCount);
    }

    [Fact]
    public void ExternalPush_RaisesChanged_ForPanelRefresh()
    {
        var stack = new UndoStack();
        var history = new UndoHistoryService(stack);
        int raised = 0;
        history.Changed += () => raised++;

        stack.Push(new StubCommand("команда инструмента"));

        Assert.True(raised >= 1);
    }
}
