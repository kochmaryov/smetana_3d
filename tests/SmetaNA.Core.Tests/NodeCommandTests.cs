using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Команды правки узлов: добавление, удаление, перенос, трансформация, имя, видимость.</summary>
public sealed class NodeCommandTests
{
    [Fact]
    public void AddNode_InsertsAtPosition_AndUndoRemoves()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B");
        scene.Root.AddChild(a);
        var stack = new UndoStack();

        stack.Push(new AddNodeCommand(scene.Root, b, 0));

        Assert.Equal([b, a], scene.Root.Children);
        Assert.Same(scene.Root, b.Parent);

        stack.Undo();
        Assert.Equal([a], scene.Root.Children);
        Assert.Null(b.Parent);

        stack.Redo();
        Assert.Equal([b, a], scene.Root.Children);
    }

    [Fact]
    public void RemoveNode_RemembersPosition_AndUndoRestoresIt()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B");
        var c = ModelTestSupport.Mesh("C");
        scene.Root.AddChild(a);
        scene.Root.AddChild(b);
        scene.Root.AddChild(c);
        var stack = new UndoStack();

        stack.Push(new RemoveNodeCommand(b));

        Assert.Equal([a, c], scene.Root.Children);
        Assert.Null(b.Parent);

        stack.Undo();
        Assert.Equal([a, b, c], scene.Root.Children);

        stack.Redo();
        Assert.Equal([a, c], scene.Root.Children);
    }

    [Fact]
    public void MoveNode_MovesToNewParent_AndUndoRestoresPosition()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B");
        var c = ModelTestSupport.Mesh("C");
        var group = ModelTestSupport.Group("G");
        scene.Root.AddChild(a);
        scene.Root.AddChild(b);
        scene.Root.AddChild(c);
        scene.Root.AddChild(group);
        var stack = new UndoStack();

        stack.Push(new MoveNodeCommand(b, group));

        Assert.Equal([a, c, group], scene.Root.Children);
        Assert.Equal([b], group.Children);

        stack.Undo();
        Assert.Equal([a, b, c, group], scene.Root.Children);
        Assert.Empty(group.Children);

        stack.Redo();
        Assert.Equal([b], group.Children);
    }

    [Fact]
    public void MoveNode_WhenTargetIsOwnDescendant_ThrowsAndKeepsTree()
    {
        var scene = new Scene();
        var group = ModelTestSupport.Group("G");
        var inner = ModelTestSupport.Group("Inner");
        scene.Root.AddChild(group);
        group.AddChild(inner);
        var command = new MoveNodeCommand(group, inner);

        Assert.Throws<InvalidOperationException>(() => command.Execute());

        Assert.Same(scene.Root, group.Parent);
        Assert.Same(group, inner.Parent);
    }

    [Fact]
    public void SetNodeTransform_UndoRedo_RestoresTransforms()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var original = mesh.LocalTransform;
        var moved = Transform3d.CreateTranslation(new Vector3d(1, 2, 3));
        var stack = new UndoStack();

        stack.Push(new SetNodeTransformCommand(mesh, moved));
        Assert.Equal(moved, mesh.LocalTransform);

        stack.Undo();
        Assert.Equal(original, mesh.LocalTransform);

        stack.Redo();
        Assert.Equal(moved, mesh.LocalTransform);
    }

    [Fact]
    public void RenameNode_UndoRedo_RestoresName()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var stack = new UndoStack();

        stack.Push(new RenameNodeCommand(mesh, "Новое имя"));
        Assert.Equal("Новое имя", mesh.Name);

        stack.Undo();
        Assert.Equal("M", mesh.Name);

        stack.Redo();
        Assert.Equal("Новое имя", mesh.Name);
    }

    [Fact]
    public void SetNodeVisibility_UndoRedo_RestoresVisibility()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var stack = new UndoStack();

        stack.Push(new SetNodeVisibilityCommand(mesh, false));
        Assert.False(mesh.IsVisible);

        stack.Undo();
        Assert.True(mesh.IsVisible);

        stack.Redo();
        Assert.False(mesh.IsVisible);
    }
}
