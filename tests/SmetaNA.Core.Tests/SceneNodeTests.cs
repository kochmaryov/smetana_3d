using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class SceneNodeTests
{
    [Fact]
    public void AddChild_SetsParentAndPreservesOrder()
    {
        var root = new GroupNode();
        var first = new GroupNode();
        var second = new GroupNode();

        root.AddChild(first);
        root.AddChild(second);

        Assert.Same(root, first.Parent);
        Assert.Same(root, second.Parent);
        Assert.Equal([first, second], root.Children);
    }

    [Fact]
    public void AddChild_WhenAlreadyParented_Throws()
    {
        var firstRoot = new GroupNode();
        var secondRoot = new GroupNode();
        var child = new GroupNode();
        firstRoot.AddChild(child);

        Assert.Throws<InvalidOperationException>(() => secondRoot.AddChild(child));
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        var root = new GroupNode();
        var child = new GroupNode();
        root.AddChild(child);

        root.RemoveChild(child);

        Assert.Null(child.Parent);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void EnumerateNodes_VisitsWholeHierarchy()
    {
        var scene = new Scene();
        var child = new GroupNode();
        var grandChild = new GroupNode();
        scene.Root.AddChild(child);
        child.AddChild(grandChild);

        var nodes = scene.EnumerateNodes().ToList();

        Assert.Contains(scene.Root, nodes);
        Assert.Contains(child, nodes);
        Assert.Contains(grandChild, nodes);
        Assert.Equal(3, nodes.Count);
    }

    [Fact]
    public void TransformPoint_AppliesTranslation()
    {
        var transform = Transform3d.CreateTranslation(new Vector3d(1, 2, 3));

        var result = transform.TransformPoint(new Vector3d(10, 20, 30));

        Assert.Equal(new Vector3d(11, 22, 33), result);
    }
}
