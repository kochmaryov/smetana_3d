using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

public sealed class MeshNormalsTests
{
    [Fact]
    public void ComputeFaceNormal_CcwTriangle_PointsAlongRightHandRule()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));

        Vector3d normal = MeshNormals.ComputeFaceNormal(mesh.Faces[0]);

        GeometryAssert.VectorEqual(Vector3d.UnitZ, normal);
    }

    [Fact]
    public void ComputeFaceNormal_CollinearTriangle_IsZero()
    {
        HalfEdgeMesh mesh = TestMeshes.Triangle(new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(2, 0, 0));

        Vector3d normal = MeshNormals.ComputeFaceNormal(mesh.Faces[0]);

        GeometryAssert.VectorEqual(Vector3d.Zero, normal);
    }

    [Fact]
    public void ComputeFaceNormals_Cube_AllPointOutward()
    {
        HalfEdgeMesh mesh = TestMeshes.Cube();
        MeshData data = mesh.ToTriangles();
        Vector3d center = new(0.5, 0.5, 0.5);

        Vector3d[] normals = MeshNormals.ComputeFaceNormals(mesh);

        Assert.Equal(data.Indices.Count / 3, normals.Length);
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            Vector3d centroid = (
                data.Positions[data.Indices[i]]
                + data.Positions[data.Indices[i + 1]]
                + data.Positions[data.Indices[i + 2]]) * (1.0 / 3.0);
            double outward = Vector3d.Dot(normals[i / 3], centroid - center);
            Assert.True(outward > 0, $"Треугольник {i / 3} смотрит внутрь куба.");
        }
    }

    [Fact]
    public void ComputeVertexNormals_CubeCorner_IsDiagonal()
    {
        // Куб из четырёхугольных граней: у каждой вершины ровно три грани,
        // вклад каждой пропорционален площади — нормаль в угол куба диагональна.
        HalfEdgeMesh mesh = TestMeshes.CubeQuads();

        Vector3d[] normals = MeshNormals.ComputeVertexNormals(mesh);

        Vector3d expected = new Vector3d(-1, -1, -1) * (1.0 / Math.Sqrt(3));
        GeometryAssert.VectorEqual(expected, normals[0]);
        GeometryAssert.VectorEqual(new Vector3d(1, 1, 1) * (1.0 / Math.Sqrt(3)), normals[6]);
    }

    [Fact]
    public void ComputeVertexNormal_IsolatedVertex_IsZero()
    {
        var mesh = new HalfEdgeMesh();
        var a = mesh.AddVertex(new Vector3d(0, 0, 0));
        var b = mesh.AddVertex(new Vector3d(1, 0, 0));
        var c = mesh.AddVertex(new Vector3d(0, 1, 0));
        mesh.AddFace([a, b, c]);
        var isolated = mesh.AddVertex(new Vector3d(5, 5, 5));

        Vector3d[] normals = MeshNormals.ComputeVertexNormals(mesh);

        Assert.Equal(4, normals.Length);
        GeometryAssert.VectorEqual(Vector3d.UnitZ, normals[0]);
        GeometryAssert.VectorEqual(Vector3d.Zero, normals[3]);
        GeometryAssert.VectorEqual(Vector3d.Zero, MeshNormals.ComputeVertexNormal(mesh, isolated));
    }

    [Fact]
    public void ComputeFaceNormals_TriangleSoup_PointsUp()
    {
        var data = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)],
            [0, 1, 2]);

        Vector3d[] normals = MeshNormals.ComputeFaceNormals(data);

        Assert.Single(normals);
        GeometryAssert.VectorEqual(Vector3d.UnitZ, normals[0]);
    }

    [Fact]
    public void ComputeVertexNormals_TriangleSoup_SmoothsSharedVertices()
    {
        // Квадрат в плоскости XY из двух треугольников: у всех вершин нормаль +Z.
        var data = new MeshData(
            [
                new Vector3d(0, 0, 0), new Vector3d(1, 0, 0),
                new Vector3d(1, 1, 0), new Vector3d(0, 1, 0),
            ],
            [0, 1, 2, 0, 2, 3]);

        Vector3d[] normals = MeshNormals.ComputeVertexNormals(data);

        Assert.Equal(4, normals.Length);
        foreach (Vector3d normal in normals)
        {
            GeometryAssert.VectorEqual(Vector3d.UnitZ, normal);
        }
    }
}
