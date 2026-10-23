using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class MeshValidationTests
{
    [Fact]
    public void Validate_Cube_IsWatertightValidAndConnected()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Equal(8, result.VertexCount);
        Assert.Equal(18, result.EdgeCount);
        Assert.Equal(12, result.FaceCount);
        Assert.Equal(36, result.HalfEdgeCount);
        Assert.Equal(2, result.EulerCharacteristic);
        Assert.True(result.IsWatertight);
        Assert.True(result.IsConnected);
        Assert.True(result.IsEulerValid);
        Assert.Empty(result.Defects);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OpenTriangle_HasNoEulerConstraint()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Equal(3, result.VertexCount);
        Assert.Equal(3, result.EdgeCount);
        Assert.Equal(1, result.FaceCount);
        Assert.Equal(1, result.EulerCharacteristic);
        Assert.False(result.IsWatertight);
        Assert.True(result.IsEulerValid);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TwoTrianglesSharingEdge_CountsFiveEdges()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(1, 1, 0));
        var d = mesh.AddVertex(new Vector3d(0, 1, 0));
        mesh.AddFace([a, b, c]);
        mesh.AddFace([a, c, d]);

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Equal(4, result.VertexCount);
        Assert.Equal(5, result.EdgeCount);
        Assert.Equal(2, result.FaceCount);
        Assert.Equal(1, result.EulerCharacteristic);
        Assert.True(result.IsConnected);
    }

    [Fact]
    public void Validate_DisjointTriangles_AreNotConnected()
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles(
            [
                new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0),
                new Vector3d(5, 0, 0), new Vector3d(6, 0, 0), new Vector3d(5, 1, 0),
            ],
            [0, 1, 2, 3, 4, 5]);

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Equal(2, result.ComponentCount);
        Assert.False(result.IsConnected);
    }

    [Fact]
    public void Validate_CollinearTriangle_ReportsZeroArea()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(2, 0, 0));

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Contains(result.Defects, d => d.FaceIndex == 0 && d.Kind == MeshDefectKind.ZeroArea);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RepeatedVertexInRing_ReportsRepeatedVertex()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(0, 1, 0));
        mesh.AddFace([a, b, a, c]);

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Contains(result.Defects, d => d.FaceIndex == 0 && d.Kind == MeshDefectKind.RepeatedVertex);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TwoCubesSharingVertex_HasOddEulerCharacteristic()
    {
        // Две твёрдые части, склеенные в одной вершине: V−E+F = 3 — нечётная,
        // что для замкнутого меша означает несогласованную топологию.
        Vector3d[] positions = new Vector3d[15];
        Array.Copy(TestMeshes.CubePositions, positions, 8);
        int[] indices = new int[TestMeshes.CubeIndices.Length * 2];
        Array.Copy(TestMeshes.CubeIndices, indices, TestMeshes.CubeIndices.Length);
        for (int i = 0; i < 7; i++)
        {
            Vector3d source = TestMeshes.CubePositions[i + 1];
            positions[i + 8] = source + new Vector3d(2, 0, 0);
        }

        int[] Cube2Indices(int[] cubeIndices)
        {
            var mapped = new int[cubeIndices.Length];
            for (int i = 0; i < cubeIndices.Length; i++)
            {
                int index = cubeIndices[i];
                mapped[i] = index == 0 ? 0 : index + 7;
            }

            return mapped;
        }

        Array.Copy(Cube2Indices(TestMeshes.CubeIndices), 0, indices, TestMeshes.CubeIndices.Length, TestMeshes.CubeIndices.Length);

        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles(positions, indices);

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.True(result.IsWatertight);
        Assert.Equal(15, result.VertexCount);
        Assert.Equal(3, result.EulerCharacteristic);
        Assert.False(result.IsEulerValid);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TwoDisjointCubes_AreValidSolids()
    {
        Vector3d[] positions = new Vector3d[16];
        Array.Copy(TestMeshes.CubePositions, positions, 8);
        for (int i = 0; i < 8; i++)
        {
            positions[i + 8] = TestMeshes.CubePositions[i] + new Vector3d(2, 0, 0);
        }

        int[] indices = new int[TestMeshes.CubeIndices.Length * 2];
        Array.Copy(TestMeshes.CubeIndices, indices, TestMeshes.CubeIndices.Length);
        for (int i = 0; i < TestMeshes.CubeIndices.Length; i++)
        {
            indices[i + TestMeshes.CubeIndices.Length] = TestMeshes.CubeIndices[i] + 8;
        }

        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles(positions, indices);

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.True(result.IsWatertight);
        Assert.Equal(2, result.ComponentCount);
        Assert.Equal(4, result.EulerCharacteristic);
        Assert.True(result.IsEulerValid);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_IsolatedVertex_IsSeparateComponent()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();
        mesh.AddVertex(new Vector3d(10, 10, 10));

        MeshValidationResult result = MeshValidator.Validate(mesh);

        Assert.Equal(2, result.ComponentCount);
        Assert.Equal(1, result.IsolatedVertexCount);
        Assert.False(result.IsConnected);
        Assert.True(result.IsEulerValid);
    }

    [Fact]
    public void Validate_EmptyMesh_HasZeroCounts()
    {
        MeshValidationResult result = MeshValidator.Validate(new HalfEdgeMesh());

        Assert.Equal(0, result.VertexCount);
        Assert.Equal(0, result.EdgeCount);
        Assert.Equal(0, result.FaceCount);
        Assert.Equal(0, result.EulerCharacteristic);
        Assert.Equal(0, result.ComponentCount);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NegativeEpsilon_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MeshValidator.Validate(TestMeshes.Cube(), -1));
    }
}
