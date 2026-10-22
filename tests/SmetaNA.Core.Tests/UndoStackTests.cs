using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class UndoStackTests
{
    private sealed class StubCommand(Action execute, Action undo, string description = "stub") : IEditCommand
    {
        public string Description => description;
        public void Execute() => execute();
        public void Undo() => undo();
    }

    [Fact]
    public void Push_ExecutesCommand()
    {
        var executed = false;
        var stack = new UndoStack();

        stack.Push(new StubCommand(() => executed = true, () => { }));

        Assert.True(executed);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_ThenRedo_ReappliesCommand()
    {
        int value = 0;
        var stack = new UndoStack();
        stack.Push(new StubCommand(() => value++, () => value--));

        stack.Undo();
        Assert.Equal(0, value);
        Assert.True(stack.CanRedo);

        stack.Redo();
        Assert.Equal(1, value);
    }

    [Fact]
    public void Push_AfterUndo_ClearsRedo()
    {
        var stack = new UndoStack();
        stack.Push(new StubCommand(() => { }, () => { }));
        stack.Undo();

        stack.Push(new StubCommand(() => { }, () => { }));

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Push_BeyondMaxDepth_DropsOldest()
    {
        int value = 0;
        var stack = new UndoStack(maxDepth: 2);
        for (int i = 0; i < 3; i++)
        {
            stack.Push(new StubCommand(() => value++, () => value--));
        }

        stack.Undo();
        stack.Undo();

        Assert.False(stack.CanUndo);
        Assert.Equal(1, value);
    }

    [Fact]
    public void Undo_OnEmptyStack_Throws()
    {
        var stack = new UndoStack();

        Assert.Throws<InvalidOperationException>(stack.Undo);
    }

    [Fact]
    public void Changed_FiresOnPushUndoRedo()
    {
        var stack = new UndoStack();
        int fired = 0;
        stack.Changed += () => fired++;
        stack.Push(new StubCommand(() => { }, () => { }));
        stack.Undo();
        stack.Redo();
        stack.Clear();

        Assert.Equal(4, fired);
    }
}
