using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Изоляция контекста редактирования (задачи 3.3–3.4): открытие/закрытие групп и компонентов.</summary>
public sealed class EditingContextTests
{
    [Fact]
    public void OpenGroup_SetsActiveNode_AndNestsContexts()
    {
        var scene = new Scene();
        var outer = ModelTestSupport.Group("Outer");
        var inner = ModelTestSupport.Group("Inner");
        scene.Root.AddChild(outer);
        outer.AddChild(inner);
        var context = new EditingContext(scene);

        Assert.Same(scene.Root, context.ActiveNode);
        Assert.False(context.IsEditing);

        context.OpenGroup(outer);
        Assert.Same(outer, context.ActiveNode);
        Assert.True(context.IsEditing);
        Assert.Equal(1, context.Depth);

        context.Open(inner);
        Assert.Same(inner, context.ActiveNode);
        Assert.Equal(2, context.Depth);

        EditContextFrame closed = context.Close();
        Assert.Same(inner, closed.Target);
        Assert.Same(outer, context.ActiveNode);

        context.CloseAll();
        Assert.Same(scene.Root, context.ActiveNode);
        Assert.False(context.IsEditing);
    }

    [Fact]
    public void Close_WhenAtRoot_Throws()
    {
        var context = new EditingContext(new Scene());

        Assert.Throws<InvalidOperationException>(() => context.Close());
    }

    [Fact]
    public void Open_WhenNodeOutsideCurrentContext_Throws()
    {
        var scene = new Scene();
        var inside = ModelTestSupport.Group("Inside");
        var outside = ModelTestSupport.Group("Outside");
        scene.Root.AddChild(inside);
        scene.Root.AddChild(outside);
        var context = new EditingContext(scene);
        context.OpenGroup(inside);

        Assert.False(context.CanOpen(outside));
        Assert.Throws<InvalidOperationException>(() => context.Open(outside));
    }

    [Fact]
    public void Open_SameGroupTwice_Throws()
    {
        var scene = new Scene();
        var group = ModelTestSupport.Group("G");
        scene.Root.AddChild(group);
        var context = new EditingContext(scene);
        context.OpenGroup(group);

        Assert.Throws<InvalidOperationException>(() => context.OpenGroup(group));
    }

    [Fact]
    public void Open_WhenNodeIsNotGroupOrInstance_Throws()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var context = new EditingContext(scene);

        Assert.False(context.CanOpen(mesh));
        Assert.Throws<InvalidOperationException>(() => context.Open(mesh));
    }

    [Fact]
    public void IsInScope_CoversInstanceContentOfContext()
    {
        var scene = new Scene();
        var meshInMaster = ModelTestSupport.Mesh("MasterMesh");
        var definition = ModelTestSupport.Definition("D", "DBody", meshInMaster);
        var group = ModelTestSupport.Group("G");
        var stranger = ModelTestSupport.Mesh("Stranger");
        scene.Root.AddChild(group);
        scene.Root.AddChild(stranger);
        var nested = ModelTestSupport.Instance(definition, "I");
        group.AddChild(nested);
        var context = new EditingContext(scene);
        context.OpenGroup(group);

        Assert.True(context.IsInScope(nested));
        Assert.True(context.IsInScope(definition.Content));
        Assert.True(context.IsInScope(meshInMaster));
        Assert.True(context.CanOpen(nested));
        Assert.False(context.IsInScope(stranger));
    }

    [Fact]
    public void OpenComponent_EditsGoToMasterContent_AndPropagateToAllInstances()
    {
        var scene = new Scene();
        var definition = ModelTestSupport.Definition("D", "DBody");
        var i1 = ModelTestSupport.Instance(definition, "I1");
        var i2 = ModelTestSupport.Instance(definition, "I2", 0, 10, 0);
        scene.Root.AddChild(i1);
        scene.Root.AddChild(i2);
        var context = new EditingContext(scene);
        var stack = new UndoStack();

        context.OpenComponent(i1);
        Assert.Same(definition.Content, context.ActiveNode);
        Assert.Same(i1, context.Current.Instance);

        var added = ModelTestSupport.Mesh("Added");
        stack.Push(new AddNodeCommand(context.ActiveNode, added));

        Assert.Equal(2, OutlinerQuery.FindByName(scene, "Added").Count);

        context.Close();
        Assert.Same(scene.Root, context.ActiveNode);
    }

    [Fact]
    public void OpenComponent_WhenDefinitionAlreadyOpen_ThrowsOnCycle()
    {
        var defA = new ComponentDefinition { Name = "A", Content = ModelTestSupport.Group("ABody") };
        var defB = new ComponentDefinition { Name = "B", Content = ModelTestSupport.Group("BBody") };
        var instB = ModelTestSupport.Instance(defB, "InstB");
        defA.Content.AddChild(instB);
        var nestedInstA = ModelTestSupport.Instance(defA, "NestedInstA");
        defB.Content.AddChild(nestedInstA);

        var scene = new Scene();
        var instA = ModelTestSupport.Instance(defA, "InstA");
        scene.Root.AddChild(instA);
        var context = new EditingContext(scene);

        context.OpenComponent(instA);
        context.OpenComponent(instB);

        Assert.Throws<InvalidOperationException>(() => context.OpenComponent(nestedInstA));
    }
}
