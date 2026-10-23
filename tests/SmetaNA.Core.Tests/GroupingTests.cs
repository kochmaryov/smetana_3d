using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Группы (задача 3.3): группировка/расгруппировка, вложенность, undo/redo.</summary>
public sealed class GroupingTests
{
    [Fact]
    public void GroupNodes_ReparentsSelectionIntoNewGroup_PreservingOrderAndWorldTransforms()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B", 1);
        var c = ModelTestSupport.Mesh("C", 2);
        scene.Root.AddChild(a);
        scene.Root.AddChild(b);
        scene.Root.AddChild(c);
        IReadOnlyList<Transform3d> worldBefore = ModelTestSupport.WorldTransformsOf(scene, a);
        var stack = new UndoStack();

        stack.Push(new GroupNodesCommand([a, c]));

        var group = Assert.IsType<GroupNode>(scene.Root.Children[0]);
        Assert.Equal("Группа", group.Name);
        Assert.Equal([group, b], scene.Root.Children);
        Assert.Equal([a, c], group.Children);
        Assert.Same(group, a.Parent);
        Assert.Same(group, c.Parent);
        Assert.Equal(Transform3d.Identity, group.LocalTransform);
        Assert.Equal(worldBefore, ModelTestSupport.WorldTransformsOf(scene, a));

        stack.Undo();
        Assert.Equal([a, b, c], scene.Root.Children);
        Assert.Null(group.Parent);
        Assert.Empty(group.Children);

        stack.Redo();
        Assert.Equal([group, b], scene.Root.Children);
        Assert.Equal([a, c], group.Children);
    }

    [Fact]
    public void GroupNodes_WithCustomName_UsesGivenName()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        scene.Root.AddChild(a);
        var stack = new UndoStack();
        var command = new GroupNodesCommand([a], "Корпус");

        stack.Push(command);

        Assert.Equal("Корпус", command.Group!.Name);
    }

    [Fact]
    public void GroupNodes_NestedGroups_KeepHierarchy_AndUngroupOuterKeepsInner()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B");
        scene.Root.AddChild(a);
        scene.Root.AddChild(b);
        var stack = new UndoStack();

        var innerCommand = new GroupNodesCommand([a], "Внутренняя");
        stack.Push(innerCommand);
        GroupNode inner = innerCommand.Group!;
        var outerCommand = new GroupNodesCommand([inner, b], "Внешняя");
        stack.Push(outerCommand);
        GroupNode outer = outerCommand.Group!;

        Assert.Equal([outer], scene.Root.Children);
        Assert.Equal([inner, b], outer.Children);
        Assert.Equal([a], inner.Children);

        stack.Push(new UngroupNodeCommand(outer));

        Assert.Equal([inner, b], scene.Root.Children);
        Assert.Equal([a], inner.Children);
        Assert.DoesNotContain(outer, scene.EnumerateNodes());
    }

    [Fact]
    public void UngroupNode_PromotesChildren_AndUndoRestoresGroup()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var b = ModelTestSupport.Mesh("B");
        var c = ModelTestSupport.Mesh("C");
        scene.Root.AddChild(a);
        scene.Root.AddChild(b);
        scene.Root.AddChild(c);
        var stack = new UndoStack();
        var groupCommand = new GroupNodesCommand([a, b]);
        stack.Push(groupCommand);
        GroupNode group = groupCommand.Group!;
        Assert.Equal([group, c], scene.Root.Children);

        stack.Push(new UngroupNodeCommand(group));

        Assert.Equal([a, b, c], scene.Root.Children);
        Assert.Null(group.Parent);
        Assert.Empty(group.Children);

        stack.Undo();
        Assert.Equal([group, c], scene.Root.Children);
        Assert.Equal([a, b], group.Children);

        stack.Redo();
        Assert.Equal([a, b, c], scene.Root.Children);
    }

    [Fact]
    public void GroupNodes_WhenParentsDiffer_Throws()
    {
        var scene = new Scene();
        var a = ModelTestSupport.Mesh("A");
        var group = ModelTestSupport.Group("G");
        var inner = ModelTestSupport.Mesh("Inner");
        scene.Root.AddChild(a);
        scene.Root.AddChild(group);
        group.AddChild(inner);
        var command = new GroupNodesCommand([a, inner]);

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void GroupNodes_WhenEmptyOrDuplicated_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GroupNodesCommand([]));

        var node = ModelTestSupport.Mesh("M");
        Assert.Throws<ArgumentException>(() => new GroupNodesCommand([node, node]));
    }

    [Fact]
    public void UngroupNode_WhenGroupNotInHierarchy_Throws()
    {
        var group = ModelTestSupport.Group("G");
        var command = new UngroupNodeCommand(group);

        Assert.Throws<InvalidOperationException>(() => command.Execute());
    }

    [Fact]
    public void GroupNodes_InsideComponentMaster_PropagatesToAllInstances()
    {
        var scene = new Scene();
        var m1 = ModelTestSupport.Mesh("M1");
        var m2 = ModelTestSupport.Mesh("M2", 1);
        var definition = ModelTestSupport.Definition("D", "DBody", m1, m2);
        scene.Root.AddChild(ModelTestSupport.Instance(definition, "I1"));
        scene.Root.AddChild(ModelTestSupport.Instance(definition, "I2", 0, 10, 0));
        var stack = new UndoStack();

        stack.Push(new GroupNodesCommand([m1, m2], "Узел"));

        Assert.Equal(2, OutlinerQuery.FindByName(scene, "Узел").Count);
        Assert.Equal(2, ModelTestSupport.WorldTransformsOf(scene, m1).Count);
    }
}
