using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class DemoSceneFactoryTests
{
    [Fact]
    public void CreateTerrainChunks_DefaultTarget_HitsFiveHundredThousandTriangles()
    {
        IReadOnlyList<MeshNode> chunks = DemoSceneFactory.CreateTerrainChunks();

        int triangles = chunks.Sum(chunk => chunk.Mesh.ToTriangles().Indices.Count / 3);
        Assert.Equal(16, chunks.Count);
        Assert.Equal(500_000, triangles);
    }

    [Fact]
    public void CreateTerrainChunks_ScalesWithTarget()
    {
        IReadOnlyList<MeshNode> chunks = DemoSceneFactory.CreateTerrainChunks(8_192, chunksPerSide: 2);

        int triangles = chunks.Sum(chunk => chunk.Mesh.ToTriangles().Indices.Count / 3);
        Assert.Equal(4, chunks.Count);
        Assert.Equal(8_192, triangles);
    }

    [Fact]
    public void CreateTerrainChunks_IsDeterministic()
    {
        IReadOnlyList<MeshNode> first = DemoSceneFactory.CreateTerrainChunks(2_000, chunksPerSide: 2);
        IReadOnlyList<MeshNode> second = DemoSceneFactory.CreateTerrainChunks(2_000, chunksPerSide: 2);

        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(
                first[i].Mesh.ToTriangles().Positions.Select(p => (p.X, p.Y, p.Z)),
                second[i].Mesh.ToTriangles().Positions.Select(p => (p.X, p.Y, p.Z)));
        }
    }

    [Fact]
    public void AddTerrainToScene_AddsChunksToRoot()
    {
        var scene = new Scene();

        IReadOnlyList<MeshNode> chunks = DemoSceneFactory.AddTerrainToScene(scene, 8_192, 2);

        Assert.Equal(chunks.Count, scene.Root.Children.Count);
        Assert.All(chunks, chunk => Assert.Same(scene.Root, chunk.Parent));
    }
}
