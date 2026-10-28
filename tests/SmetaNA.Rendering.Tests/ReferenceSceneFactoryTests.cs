using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class ReferenceSceneFactoryTests
{
    [Fact]
    public void AddToScene_AddsPlateBodyCylinderToRoot()
    {
        var scene = new Scene();

        IReadOnlyList<MeshNode> nodes = ReferenceSceneFactory.AddToScene(scene);

        Assert.Equal(3, nodes.Count);
        Assert.Equal(nodes.Count, scene.Root.Children.Count);
        Assert.All(nodes, node => Assert.Same(scene.Root, node.Parent));
        Assert.Contains(nodes, node => node.Name == ReferenceSceneFactory.PlateNodeName);
        Assert.Contains(nodes, node => node.Name == ReferenceSceneFactory.BodyNodeName);
        Assert.Contains(nodes, node => node.Name == ReferenceSceneFactory.CylinderNodeName);
    }

    [Fact]
    public void AddToScene_StaysLightUnderTwoHundredTriangles()
    {
        var scene = new Scene();

        IReadOnlyList<MeshNode> nodes = ReferenceSceneFactory.AddToScene(scene);

        int triangles = nodes.Sum(node => node.Mesh.ToTriangles().Indices.Count / 3);
        Assert.InRange(triangles, 1, 200);
    }

    [Fact]
    public void AddToScene_PlateTopLiesOnGroundPlane()
    {
        var scene = new Scene();

        IReadOnlyList<MeshNode> nodes = ReferenceSceneFactory.AddToScene(scene);
        MeshNode plate = nodes.Single(node => node.Name == ReferenceSceneFactory.PlateNodeName);
        IReadOnlyList<Vector3d> positions = plate.Mesh.ToTriangles().Positions;

        Assert.Equal(0, positions.Max(p => p.Z));
        Assert.Equal(-10, positions.Min(p => p.Z));
    }

    [Fact]
    public void AddToScene_BodyAndCylinderStandOnPlate()
    {
        var scene = new Scene();

        IReadOnlyList<MeshNode> nodes = ReferenceSceneFactory.AddToScene(scene);

        foreach (string name in new[] { ReferenceSceneFactory.BodyNodeName, ReferenceSceneFactory.CylinderNodeName })
        {
            MeshNode node = nodes.Single(candidate => candidate.Name == name);
            Assert.Equal(0, node.Mesh.ToTriangles().Positions.Min(p => p.Z));
        }
    }

    [Fact]
    public void AddToScene_IsDeterministic()
    {
        var first = ReferenceSceneFactory.AddToScene(new Scene());
        var second = ReferenceSceneFactory.AddToScene(new Scene());

        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(
                first[i].Mesh.ToTriangles().Positions.Select(p => (p.X, p.Y, p.Z)),
                second[i].Mesh.ToTriangles().Positions.Select(p => (p.X, p.Y, p.Z)));
        }
    }
}
