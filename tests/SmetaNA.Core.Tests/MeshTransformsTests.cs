using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class MeshTransformsTests
{
    [Fact]
    public void TransformInPlace_MovesVertices_PreservesTopology()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();

        MeshTransforms.TransformInPlace(mesh, Transform3d.CreateTranslation(new Vector3d(10, -5, 2)));

        Assert.Equal(8, mesh.Vertices.Count);
        Assert.True(mesh.IsWatertight);
        GeometryAssert.VectorEqual(new Vector3d(10, -5, 2), mesh.Vertices[0].Position);
        GeometryAssert.VectorEqual(new Vector3d(10, -4, 3), mesh.Vertices[7].Position);
    }

    [Fact]
    public void Transformed_CreatesIndependentCopy()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));

        HalfEdgeMesh copy = MeshTransforms.Transformed(mesh, Transform3d.CreateTranslation(new Vector3d(0, 0, 5)));

        GeometryAssert.VectorEqual(Vector3d.Zero, mesh.Vertices[0].Position);
        GeometryAssert.VectorEqual(new Vector3d(0, 0, 5), copy.Vertices[0].Position);
        Assert.Equal(mesh.Faces.Count, copy.Faces.Count);
    }

    [Fact]
    public void Transformed_PreservesNgonFaces()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(1, 1, 0));
        var d = mesh.AddVertex(new Vector3d(0, 1, 0));
        mesh.AddFace([a, b, c, d]);

        HalfEdgeMesh copy = MeshTransforms.Transformed(mesh, TestMeshes.RotationZ(Math.PI / 2));

        Assert.Single(copy.Faces);
        Assert.Equal(4, copy.HalfEdges.Count);
        GeometryAssert.VectorEqual(new Vector3d(0, 1, 0), copy.Vertices[1].Position);
    }

    [Fact]
    public void Transformed_PreservesMaterialIndexAndWatertightness()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();
        mesh.Faces[0].MaterialIndex = 7;

        HalfEdgeMesh copy = MeshTransforms.Transformed(mesh, TestMeshes.Scale(1, 2, 3));

        Assert.True(copy.IsWatertight);
        Assert.Equal(7, copy.Faces[0].MaterialIndex);
        Assert.Equal(mesh.Faces.Count, copy.Faces.Count);
    }

    [Fact]
    public void Transformed_NonUniformScale_MovesVerticesPerAxis()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(1, 2, 3), new Vector3d(-1, 0.5, 0), new Vector3d(0, 0, 2));

        HalfEdgeMesh copy = MeshTransforms.Transformed(mesh, TestMeshes.Scale(2, 3, 4));

        GeometryAssert.VectorEqual(new Vector3d(2, 6, 12), copy.Vertices[0].Position);
        GeometryAssert.VectorEqual(new Vector3d(-2, 1.5, 0), copy.Vertices[1].Position);
        GeometryAssert.VectorEqual(new Vector3d(0, 0, 8), copy.Vertices[2].Position);
    }

    [Fact]
    public void Transformed_TriangleSoup_KeepsIndices()
    {
        var data = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)],
            [0, 1, 2]);

        MeshData moved = MeshTransforms.Transformed(data, Transform3d.CreateTranslation(new Vector3d(5, 0, 0)));

        Assert.Equal(data.Indices, moved.Indices);
        GeometryAssert.VectorEqual(new Vector3d(5, 0, 0), moved.Positions[0]);
    }
}
