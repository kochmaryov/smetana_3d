using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Rendering;
using Xunit;

namespace SmetaNA.Rendering.Tests;

public sealed class EdgeExtractorTests
{
    [Fact]
    public void FromHalfEdgeMesh_Box_HasTwelveEdgesWithoutDiagonals()
    {
        HalfEdgeMesh box = TestMeshes.Box();

        int[] edges = EdgeExtractor.FromHalfEdgeMesh(box);

        // 8 вершин, 12 рёбер куба; веерная триангуляция добавила бы диагонали.
        Assert.Equal(24, edges.Length);
        Assert.Equal(12, edges.Length / 2);
    }

    [Fact]
    public void FromHalfEdgeMesh_IndicesMatchToTrianglesOrder()
    {
        HalfEdgeMesh box = TestMeshes.Box();
        MeshData triangles = box.ToTriangles();

        int[] edges = EdgeExtractor.FromHalfEdgeMesh(box);

        for (int i = 0; i < edges.Length; i++)
        {
            Assert.InRange(edges[i], 0, triangles.Positions.Count - 1);
        }
    }

    [Fact]
    public void FromTriangles_DeduplicatesSharedEdges()
    {
        // Два треугольника с общим ребром: 5 уникальных рёбер вместо 6.
        var data = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(1, 1, 0), new Vector3d(0, 1, 0)],
            [0, 1, 2, 0, 2, 3]);

        int[] edges = EdgeExtractor.FromTriangles(data);

        Assert.Equal(10, edges.Length);
    }

    [Fact]
    public void FromTriangles_SingleTriangle_HasThreeEdges()
    {
        var data = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)],
            [0, 1, 2]);

        int[] edges = EdgeExtractor.FromTriangles(data);

        Assert.Equal(6, edges.Length);
    }
}
