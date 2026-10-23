using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class SceneRenderModelTests
{
    private static MeshNode BoxNode(string name, Vector3d offset)
    {
        HalfEdgeMesh box = TestMeshes.Box();
        return new MeshNode
        {
            Mesh = box,
            Name = name,
            LocalTransform = Transform3d.CreateTranslation(offset),
        };
    }

    [Fact]
    public void Build_TopLevelMesh_SelectsItself()
    {
        var scene = new Scene();
        MeshNode node = BoxNode("box", Vector3d.Zero);
        scene.Root.AddChild(node);

        SceneRenderModel model = SceneRenderModel.Build(scene);

        RenderObject obj = Assert.Single(model.Objects);
        Assert.Same(node, obj.SelectionNode);
        Assert.Equal(12, obj.TriangleCount); // куб → 6 квадов → 12 треугольников
    }

    [Fact]
    public void Build_GroupedMesh_SelectsGroup()
    {
        var scene = new Scene();
        var group = new GroupNode { Name = "group" };
        MeshNode node = BoxNode("box", Vector3d.Zero);
        group.AddChild(node);
        scene.Root.AddChild(group);

        SceneRenderModel model = SceneRenderModel.Build(scene);

        RenderObject obj = Assert.Single(model.Objects);
        Assert.Same(group, obj.SelectionNode);
    }

    [Fact]
    public void Build_NestedGroups_SelectsNearestGroup()
    {
        var scene = new Scene();
        var outer = new GroupNode { Name = "outer" };
        var inner = new GroupNode { Name = "inner" };
        MeshNode node = BoxNode("box", Vector3d.Zero);
        inner.AddChild(node);
        outer.AddChild(inner);
        scene.Root.AddChild(outer);

        SceneRenderModel model = SceneRenderModel.Build(scene);

        RenderObject obj = Assert.Single(model.Objects);
        Assert.Same(inner, obj.SelectionNode);
    }

    [Fact]
    public void Build_ComponentInstance_SelectsInstance_NotDefinitionContent()
    {
        var scene = new Scene();
        var definition = new ComponentDefinition { Name = "comp" };
        var content = new GroupNode { Name = "content" };
        content.AddChild(BoxNode("box", Vector3d.Zero));
        definition.Content = content;
        var instance = new ComponentInstanceNode
        {
            Definition = definition,
            Name = "instance",
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(1000, 0, 0)),
        };
        scene.Root.AddChild(instance);

        SceneRenderModel model = SceneRenderModel.Build(scene);

        RenderObject obj = Assert.Single(model.Objects);
        Assert.Same(instance, obj.SelectionNode);

        // Геометрия инстанса сдвинута его мировой трансформацией.
        Assert.Equal(950, obj.Bounds.Min.X, 6);
    }

    [Fact]
    public void Build_HiddenSubtree_SkippedByDefault()
    {
        var scene = new Scene();
        MeshNode visible = BoxNode("visible", Vector3d.Zero);
        MeshNode hidden = BoxNode("hidden", new Vector3d(500, 0, 0));
        hidden.IsVisible = false;
        scene.Root.AddChild(visible);
        scene.Root.AddChild(hidden);

        SceneRenderModel model = SceneRenderModel.Build(scene);
        Assert.Single(model.Objects);

        SceneRenderModel withHidden = SceneRenderModel.Build(scene, includeHidden: true);
        Assert.Equal(2, withHidden.Objects.Count);
    }

    [Fact]
    public void Build_AggregatesBoundsAndCounts()
    {
        var scene = new Scene();
        scene.Root.AddChild(BoxNode("a", Vector3d.Zero));
        scene.Root.AddChild(BoxNode("b", new Vector3d(1000, 0, 0)));

        SceneRenderModel model = SceneRenderModel.Build(scene);

        Assert.Equal(2, model.Objects.Count);
        Assert.Equal(24, model.TriangleCount);
        Assert.Equal(24, model.EdgeCount); // по 12 рёбер на куб
        Assert.Equal(-50, model.Bounds.Min.X, 6);
        Assert.Equal(1050, model.Bounds.Max.X, 6);
    }
}
