using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class HalfEdgeMeshTests
{
    private static readonly Vector3d[] CubePositions =
    [
        new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    ];

    private static readonly int[] CubeIndices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
    ];

    [Fact]
    public void AddFace_LinksRingAndPairsTwins()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(1, 1, 0));
        var d = mesh.AddVertex(new Vector3d(0, 1, 0));

        mesh.AddFace([a, b, c]);
        mesh.AddFace([a, c, d]);

        Assert.Equal(4, mesh.Vertices.Count);
        Assert.Equal(6, mesh.HalfEdges.Count);
        Assert.Equal(2, mesh.Faces.Count);
        Assert.Equal(1, mesh.HalfEdges.Count(e => e.Twin is not null) / 2);
    }

    [Fact]
    public void FromTriangles_Cube_IsWatertight()
    {
        var mesh = HalfEdgeMesh.FromTriangles(CubePositions, CubeIndices);

        Assert.Equal(8, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Faces.Count);
        Assert.Equal(36, mesh.HalfEdges.Count);
        Assert.True(mesh.IsWatertight);
    }

    [Fact]
    public void FromTriangles_OpenMesh_IsNotWatertight()
    {
        var mesh = HalfEdgeMesh.FromTriangles(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(1, 1, 0)],
            [0, 1, 2]);

        Assert.False(mesh.IsWatertight);
    }

    [Fact]
    public void ToTriangles_RoundTrip_PreservesTopology()
    {
        var mesh = HalfEdgeMesh.FromTriangles(CubePositions, CubeIndices);

        MeshData data = mesh.ToTriangles();
        var rebuilt = HalfEdgeMesh.FromTriangles(data.Positions, data.Indices);

        Assert.Equal(CubePositions.Length, data.Positions.Count);
        Assert.Equal(CubeIndices.Length, data.Indices.Count);
        Assert.Equal(mesh.Faces.Count, rebuilt.Faces.Count);
        Assert.True(rebuilt.IsWatertight);
    }

    [Fact]
    public void AddFace_DegenerateRing_Throws()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));

        Assert.Throws<ArgumentException>(() => mesh.AddFace([a, b]));
    }

    [Fact]
    public void AddFace_NonManifoldEdge_Throws()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(1, 1, 0));
        var d = mesh.AddVertex(new Vector3d(0, 1, 1));
        var e = mesh.AddVertex(new Vector3d(0, -1, -1));
        mesh.AddFace([a, b, c]);
        mesh.AddFace([a, c, d]);

        Assert.Throws<InvalidOperationException>(() => mesh.AddFace([a, c, e]));
    }
}
