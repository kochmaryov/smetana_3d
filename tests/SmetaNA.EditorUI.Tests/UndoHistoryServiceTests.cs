using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Sessions;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

public sealed class UndoHistoryServiceTests
{
    private sealed class StubCommand(string description) : IEditCommand
    {
        public string Description => description;
        public void Execute() { }
        public void Undo() { }
    }

    [Fact]
    public void Execute_AddsAppliedEntry_AndUpdatesStack()
    {
        var service = new UndoHistoryService(new UndoStack());

        service.Execute(new StubCommand("первое"));

        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Equal("первое", service.NextUndoDescription);
        IReadOnlyList<UndoHistoryEntry> entries = service.Entries;
        Assert.Single(entries);
        Assert.True(entries[0].IsApplied);
        Assert.Equal(1, service.EntriesAppliedCount);
    }

    [Fact]
    public void Undo_ThenRedo_MovesCurrentStateMarker()
    {
        var service = new UndoHistoryService(new UndoStack());
        service.Execute(new StubCommand("первое"));

        service.Undo();
        Assert.False(service.Entries[0].IsApplied);
        Assert.Equal(0, service.EntriesAppliedCount);
        Assert.True(service.CanRedo);

        service.Redo();
        Assert.True(service.Entries[0].IsApplied);
        Assert.Equal(1, service.EntriesAppliedCount);
    }

    [Fact]
    public void Execute_AfterUndo_DropsRedoTail()
    {
        var service = new UndoHistoryService(new UndoStack());
        service.Execute(new StubCommand("первое"));
        service.Execute(new StubCommand("второе"));
        service.Undo();

        service.Execute(new StubCommand("третье"));

        IReadOnlyList<UndoHistoryEntry> entries = service.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal("первое", entries[0].Description);
        Assert.Equal("третье", entries[1].Description);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Undo_WithoutCommands_DoesNothing()
    {
        var service = new UndoHistoryService(new UndoStack());

        service.Undo();
        service.Redo();

        Assert.Empty(service.Entries);
    }

    [Fact]
    public void History_TrimsToMaxDepth()
    {
        var service = new UndoHistoryService(new UndoStack(maxDepth: 2), maxDepth: 2);
        service.Execute(new StubCommand("1"));
        service.Execute(new StubCommand("2"));
        service.Execute(new StubCommand("3"));

        IReadOnlyList<UndoHistoryEntry> entries = service.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal("2", entries[0].Description);
        Assert.Equal("3", entries[1].Description);
        Assert.Equal(2, service.EntriesAppliedCount);
    }

    [Fact]
    public void Clear_EmptiesHistory()
    {
        var service = new UndoHistoryService(new UndoStack());
        service.Execute(new StubCommand("первое"));

        service.Clear();

        Assert.Empty(service.Entries);
        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void NextUndoDescription_ComesFromCoreStack()
    {
        var stack = new UndoStack();
        var service = new UndoHistoryService(stack);
        service.Execute(new StubCommand("описание из Core"));

        Assert.Equal(stack.NextUndoDescription, service.NextUndoDescription);
    }
}
