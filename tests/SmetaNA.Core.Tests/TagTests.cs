using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Теги/слои (задача 3.5): модель, привязка, наследование видимости, переименование.</summary>
public sealed class TagTests
{
    [Fact]
    public void AssignTag_BindsNode_AndUndoRestoresPreviousTag()
    {
        var scene = new Scene();
        var first = scene.Tags.Create("Первый");
        var second = scene.Tags.Create("Второй");
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var stack = new UndoStack();

        stack.Push(new AssignTagCommand(mesh, first));
        Assert.Same(first, mesh.Tag);

        stack.Push(new AssignTagCommand(mesh, second));
        Assert.Same(second, mesh.Tag);

        stack.Undo();
        Assert.Same(first, mesh.Tag);

        stack.Push(new AssignTagCommand(mesh, null));
        Assert.Null(mesh.Tag);
    }

    [Fact]
    public void HiddenTag_HidesSubtree_ButNotSiblings()
    {
        var scene = new Scene();
        var tag = scene.Tags.Create("Крыша");
        var group = ModelTestSupport.Group("G");
        var child = ModelTestSupport.Mesh("Child");
        var sibling = ModelTestSupport.Mesh("Sibling");
        scene.Root.AddChild(group);
        group.AddChild(child);
        scene.Root.AddChild(sibling);
        var stack = new UndoStack();
        stack.Push(new AssignTagCommand(group, tag));

        stack.Push(new SetTagVisibilityCommand(tag, false));

        Assert.False(SceneVisibility.IsEffectivelyVisible(group));
        Assert.False(SceneVisibility.IsEffectivelyVisible(child));
        Assert.True(SceneVisibility.IsEffectivelyVisible(sibling));

        stack.Undo();
        Assert.True(SceneVisibility.IsEffectivelyVisible(child));
    }

    [Fact]
    public void HiddenParentTag_HidesChild_EvenWithOwnVisibleTag()
    {
        var scene = new Scene();
        var parentTag = scene.Tags.Create("Родитель");
        var childTag = scene.Tags.Create("Ребёнок");
        var group = ModelTestSupport.Group("G");
        var child = ModelTestSupport.Mesh("Child");
        scene.Root.AddChild(group);
        group.AddChild(child);
        var stack = new UndoStack();
        stack.Push(new AssignTagCommand(group, parentTag));
        stack.Push(new AssignTagCommand(child, childTag));

        stack.Push(new SetTagVisibilityCommand(parentTag, false));

        Assert.False(SceneVisibility.IsEffectivelyVisible(child));
    }

    [Fact]
    public void HiddenTagOnChild_DoesNotHideParent()
    {
        var scene = new Scene();
        var tag = scene.Tags.Create("Тег");
        var group = ModelTestSupport.Group("G");
        var child = ModelTestSupport.Mesh("Child");
        scene.Root.AddChild(group);
        group.AddChild(child);
        var stack = new UndoStack();
        stack.Push(new AssignTagCommand(child, tag));

        stack.Push(new SetTagVisibilityCommand(tag, false));

        Assert.False(SceneVisibility.IsEffectivelyVisible(child));
        Assert.True(SceneVisibility.IsEffectivelyVisible(group));
    }

    [Fact]
    public void HiddenInstance_HidesItsContentPerAccessPath()
    {
        var meshInMaster = ModelTestSupport.Mesh("MasterMesh");
        var definition = ModelTestSupport.Definition("D", "DBody", meshInMaster);
        var i1 = ModelTestSupport.Instance(definition, "I1");
        var i2 = ModelTestSupport.Instance(definition, "I2", 0, 10, 0);
        var scene = new Scene();
        scene.Root.AddChild(i1);
        scene.Root.AddChild(i2);
        var stack = new UndoStack();

        stack.Push(new SetNodeVisibilityCommand(i1, false));

        List<OutlinerEntry> entries = OutlinerQuery.Walk(scene)
            .Where(e => ReferenceEquals(e.Node, meshInMaster))
            .ToList();
        Assert.Equal(new[] { false, true }, entries.Select(e => e.IsEffectivelyVisible).ToArray());

        stack.Undo();
        entries = OutlinerQuery.Walk(scene)
            .Where(e => ReferenceEquals(e.Node, meshInMaster))
            .ToList();
        Assert.Equal(new[] { true, true }, entries.Select(e => e.IsEffectivelyVisible).ToArray());
    }

    [Fact]
    public void HiddenTagOnInstance_HidesItsContentPerAccessPath()
    {
        var meshInMaster = ModelTestSupport.Mesh("MasterMesh");
        var definition = ModelTestSupport.Definition("D", "DBody", meshInMaster);
        var i1 = ModelTestSupport.Instance(definition, "I1");
        var i2 = ModelTestSupport.Instance(definition, "I2", 0, 10, 0);
        var scene = new Scene();
        scene.Root.AddChild(i1);
        scene.Root.AddChild(i2);
        var tag = scene.Tags.Create("Скрытый");
        var stack = new UndoStack();
        stack.Push(new AssignTagCommand(i1, tag));

        stack.Push(new SetTagVisibilityCommand(tag, false));

        List<OutlinerEntry> entries = OutlinerQuery.Walk(scene)
            .Where(e => ReferenceEquals(e.Node, meshInMaster))
            .ToList();
        Assert.Equal(new[] { false, true }, entries.Select(e => e.IsEffectivelyVisible).ToArray());
    }

    [Fact]
    public void RenameTag_UpdatesName_AndUndoRestores()
    {
        var scene = new Scene();
        var tag = scene.Tags.Create("Было");
        var stack = new UndoStack();

        stack.Push(new RenameTagCommand(scene.Tags, tag, "Стало"));
        Assert.Equal("Стало", tag.Name);
        Assert.Same(tag, scene.Tags.FindByName("стало"));

        stack.Undo();
        Assert.Equal("Было", tag.Name);
    }

    [Fact]
    public void RenameTag_ToDuplicateOrEmptyName_Throws()
    {
        var scene = new Scene();
        scene.Tags.Create("Первый");
        var tag = scene.Tags.Create("Второй");
        var stack = new UndoStack();

        Assert.Throws<InvalidOperationException>(
            () => stack.Push(new RenameTagCommand(scene.Tags, tag, "первый")));
        Assert.Throws<ArgumentException>(
            () => stack.Push(new RenameTagCommand(scene.Tags, tag, " ")));
    }

    [Fact]
    public void CreateAndDeleteTag_UndoRestoresTagAndBindings()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(mesh);
        var stack = new UndoStack();
        var create = new CreateTagCommand(scene.Tags, "Слой1");
        stack.Push(create);
        Tag tag = create.Tag!;
        stack.Push(new AssignTagCommand(mesh, tag));

        stack.Push(new DeleteTagCommand(scene, tag));

        Assert.Null(mesh.Tag);
        Assert.Empty(scene.Tags.Tags);

        stack.Undo();
        Assert.Same(tag, mesh.Tag);
        Assert.Same(tag, scene.Tags.FindByName("Слой1"));

        stack.Undo();
        stack.Undo();
        Assert.Empty(scene.Tags.Tags);
    }

    [Fact]
    public void TagTable_RejectsDuplicateNames()
    {
        var table = new TagTable();
        table.Create("Тег");

        Assert.Throws<InvalidOperationException>(() => table.Create("тег"));
        Assert.Throws<ArgumentException>(() => table.Create("  "));
    }
}
