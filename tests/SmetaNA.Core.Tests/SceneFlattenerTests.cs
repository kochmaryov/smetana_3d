using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class SceneFlattenerTests
{
    private static MeshNode TriangleNode(double x = 0, double y = 0, double z = 0) => new()
    {
        Name = "Triangle",
        Mesh = TestMeshes.Triangle(
            new Vector3d(0, 0, 0),
            new Vector3d(1, 0, 0),
            new Vector3d(0, 1, 0)),
        LocalTransform = Transform3d.CreateTranslation(new Vector3d(x, y, z)),
    };

    [Fact]
    public void Flatten_CombinesMeshesWithTheirWorldTransforms()
    {
        var scene = new Scene();
        scene.Root.AddChild(TriangleNode());
        scene.Root.AddChild(TriangleNode(0, 20, 0));

        MeshData world = SceneFlattener.Flatten(scene);

        Assert.Equal(6, world.Positions.Count);
        Assert.Equal(6, world.Indices.Count);
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(0, 20, 0));
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(1, 20, 0));
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(0, 0, 0));
    }

    [Fact]
    public void Flatten_ComposesNestedTransformsWithRotation()
    {
        var scene = new Scene();
        var group = new GroupNode { LocalTransform = TestMeshes.RotationZ(Math.PI / 2) };
        group.AddChild(TriangleNode(1, 0, 0));
        scene.Root.AddChild(group);

        MeshData world = SceneFlattener.Flatten(scene);

        // Треугольник смещён на (1,0,0) в системе группы; группа повёрнута на 90° вокруг Z.
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(0, 1, 0));
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(0, 2, 0));
    }

    [Fact]
    public void Flatten_Instance_EmitsGeometryOncePerInstance()
    {
        var scene = new Scene();
        var definition = new ComponentDefinition { Name = "Part", Content = TriangleNode() };
        scene.Root.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(10, 0, 0)),
        });
        scene.Root.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(20, 0, 0)),
        });

        MeshData world = SceneFlattener.Flatten(scene);

        Assert.Equal(6, world.Positions.Count);
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(10, 0, 0));
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(21, 0, 0));
    }

    [Fact]
    public void Flatten_InstanceContentTransform_ComposesWithInstanceTransform()
    {
        var scene = new Scene();
        var content = new GroupNode { LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 5, 0)) };
        content.AddChild(TriangleNode());
        var definition = new ComponentDefinition { Name = "Part", Content = content };
        scene.Root.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(10, 0, 0)),
        });

        MeshData world = SceneFlattener.Flatten(scene);

        // Содержимое определения живёт в системе координат инстанса:
        // сначала локальный сдвиг контента, затем трансформация инстанса.
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(10, 5, 0));
    }

    [Fact]
    public void Flatten_NestedInstances_ComposeTransforms()
    {
        var scene = new Scene();
        var inner = new ComponentDefinition { Name = "Inner", Content = TriangleNode() };
        var outerContent = new GroupNode { LocalTransform = Transform3d.CreateTranslation(new Vector3d(0, 1, 0)) };
        outerContent.AddChild(new ComponentInstanceNode { Definition = inner });
        var outer = new ComponentDefinition { Name = "Outer", Content = outerContent };
        scene.Root.AddChild(new ComponentInstanceNode
        {
            Definition = outer,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(1, 0, 0)),
        });

        MeshData world = SceneFlattener.Flatten(scene);

        Assert.Equal(3, world.Positions.Count);
        GeometryAssert.ContainsVector(world.Positions, new Vector3d(1, 1, 0));
    }

    [Fact]
    public void Flatten_CyclicDefinition_Throws()
    {
        var scene = new Scene();
        var definition = new ComponentDefinition { Name = "Loop" };
        var content = new GroupNode();
        content.AddChild(new ComponentInstanceNode { Definition = definition });
        definition.Content = content;
        scene.Root.AddChild(new ComponentInstanceNode { Definition = definition });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => SceneFlattener.Flatten(scene));
        Assert.Contains("Циклическая ссылка", error.Message);
    }

    [Fact]
    public void Flatten_HiddenSubtree_SkippedUnlessRequested()
    {
        var scene = new Scene();
        scene.Root.AddChild(TriangleNode());
        var hidden = TriangleNode();
        hidden.IsVisible = false;
        scene.Root.AddChild(hidden);

        MeshData visible = SceneFlattener.Flatten(scene);
        MeshData all = SceneFlattener.Flatten(scene, includeHidden: true);

        Assert.Equal(3, visible.Positions.Count);
        Assert.Equal(6, all.Positions.Count);
    }

    [Fact]
    public void Flatten_EmptyScene_ReturnsEmptySoup()
    {
        MeshData world = SceneFlattener.Flatten(new Scene());

        Assert.Empty(world.Positions);
        Assert.Empty(world.Indices);
    }

    [Fact]
    public void Expand_ReportsWorldTransformsInPreOrder()
    {
        var scene = new Scene();
        var group = new GroupNode { LocalTransform = Transform3d.CreateTranslation(new Vector3d(10, 0, 0)) };
        var meshNode = TriangleNode(0, 5, 0);
        group.AddChild(meshNode);
        scene.Root.AddChild(group);

        IReadOnlyList<PlacedNode> placed = SceneFlattener.Expand(scene);

        Assert.Equal(3, placed.Count);
        Assert.Same(scene.Root, placed[0].Node);
        GeometryAssert.VectorEqual(Vector3d.Zero, placed[0].WorldTransform.TransformPoint(Vector3d.Zero));
        Assert.Same(group, placed[1].Node);
        GeometryAssert.VectorEqual(new Vector3d(10, 0, 0), placed[1].WorldTransform.TransformPoint(Vector3d.Zero));
        Assert.Same(meshNode, placed[2].Node);
        GeometryAssert.VectorEqual(new Vector3d(10, 5, 0), placed[2].WorldTransform.TransformPoint(Vector3d.Zero));
    }

    [Fact]
    public void Expand_EmitsInstanceContentOncePerInstance()
    {
        var scene = new Scene();
        var content = new GroupNode();
        content.AddChild(TriangleNode());
        var definition = new ComponentDefinition { Name = "Part", Content = content };
        scene.Root.AddChild(new ComponentInstanceNode { Definition = definition });
        scene.Root.AddChild(new ComponentInstanceNode
        {
            Definition = definition,
            LocalTransform = Transform3d.CreateTranslation(new Vector3d(5, 0, 0)),
        });

        IReadOnlyList<PlacedNode> placed = SceneFlattener.Expand(scene);

        // Root + (инстанс + контент + меш) × 2
        Assert.Equal(7, placed.Count);
        var meshes = placed.Where(p => p.Node is MeshNode).ToList();
        Assert.Equal(2, meshes.Count);
        GeometryAssert.VectorEqual(Vector3d.Zero, meshes[0].WorldTransform.TransformPoint(Vector3d.Zero));
        GeometryAssert.VectorEqual(new Vector3d(5, 0, 0), meshes[1].WorldTransform.TransformPoint(Vector3d.Zero));
    }
}
