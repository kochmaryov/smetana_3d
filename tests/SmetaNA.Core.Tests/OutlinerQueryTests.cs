using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Запросы для панели Outliner (задача 3.5): обход, поиск по имени, переименование, путь.</summary>
public sealed class OutlinerQueryTests
{
    [Fact]
    public void Walk_VisitsTreeWithDepths_IncludingDefinitionContent()
    {
        var scene = new Scene();
        var g1 = ModelTestSupport.Group("G1");
        var m1 = ModelTestSupport.Mesh("M1");
        scene.Root.AddChild(g1);
        g1.AddChild(m1);
        var definition = ModelTestSupport.Definition("D", "CBody", ModelTestSupport.Mesh("CM"));
        scene.Root.AddChild(ModelTestSupport.Instance(definition, "I1"));

        var entries = OutlinerQuery.Walk(scene).ToList();

        Assert.Equal(
            new[] { ("Root", 0), ("G1", 1), ("M1", 2), ("I1", 1), ("CBody", 2), ("CM", 3) },
            entries.Select(e => (e.Node.Name, e.Depth)).ToArray());
        Assert.All(entries, e => Assert.True(e.IsEffectivelyVisible));
    }

    [Fact]
    public void FindByName_MatchesSubstring_CaseInsensitive()
    {
        var scene = new Scene();
        scene.Root.AddChild(ModelTestSupport.Mesh("Корпус-1"));
        scene.Root.AddChild(ModelTestSupport.Mesh("Крышка"));
        scene.Root.AddChild(ModelTestSupport.Mesh("корпус-2"));

        Assert.Equal(2, OutlinerQuery.FindByName(scene, "КОРПУС").Count);
        Assert.Single(OutlinerQuery.FindByName(scene, "Крышка"));
        Assert.Empty(OutlinerQuery.FindByName(scene, "нет такого"));
    }

    [Fact]
    public void FindByName_EmptyPattern_ReturnsWholeTree()
    {
        var scene = new Scene();
        scene.Root.AddChild(ModelTestSupport.Mesh("A"));
        scene.Root.AddChild(ModelTestSupport.Mesh("B"));

        Assert.Equal(3, OutlinerQuery.FindByName(scene, string.Empty).Count);
    }

    [Fact]
    public void FindByName_FindsMasterContentOncePerInstance()
    {
        var meshInMaster = ModelTestSupport.Mesh("Bolt");
        var definition = ModelTestSupport.Definition("D", "DBody", meshInMaster);
        var scene = new Scene();
        scene.Root.AddChild(ModelTestSupport.Instance(definition, "I1"));
        scene.Root.AddChild(ModelTestSupport.Instance(definition, "I2", 0, 10, 0));

        Assert.Equal(2, OutlinerQuery.FindByName(scene, "Bolt").Count);
    }

    [Fact]
    public void RenameNode_ChangesSearchResults_AndUndoRestores()
    {
        var scene = new Scene();
        var mesh = ModelTestSupport.Mesh("M1");
        scene.Root.AddChild(mesh);
        var stack = new UndoStack();

        stack.Push(new RenameNodeCommand(mesh, "Новый"));

        Assert.Single(OutlinerQuery.FindByName(scene, "Новый"));
        Assert.Empty(OutlinerQuery.FindByName(scene, "M1"));

        stack.Undo();
        Assert.Single(OutlinerQuery.FindByName(scene, "M1"));
    }

    [Fact]
    public void GetPath_ReturnsChainFromTopToNode()
    {
        var scene = new Scene();
        var group = ModelTestSupport.Group("G");
        var mesh = ModelTestSupport.Mesh("M");
        scene.Root.AddChild(group);
        group.AddChild(mesh);

        IReadOnlyList<SceneNode> path = OutlinerQuery.GetPath(mesh);

        Assert.Equal([scene.Root, group, mesh], path);
    }

    [Fact]
    public void Walk_WhenDefinitionsAreCyclic_Throws()
    {
        var defA = new ComponentDefinition { Name = "A", Content = ModelTestSupport.Group("ABody") };
        var defB = new ComponentDefinition { Name = "B", Content = ModelTestSupport.Group("BBody") };
        defA.Content.AddChild(ModelTestSupport.Instance(defB, "InstB"));
        defB.Content.AddChild(ModelTestSupport.Instance(defA, "InstA"));

        var scene = new Scene();
        scene.Root.AddChild(ModelTestSupport.Instance(defA, "RootA"));

        Assert.Throws<InvalidOperationException>(() => OutlinerQuery.Walk(scene).ToList());
    }
}
