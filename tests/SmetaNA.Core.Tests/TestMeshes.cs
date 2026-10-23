using SmetaNA.Core.Geometry;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>Общие тестовые модели и матрицы.</summary>
internal static class TestMeshes
{
    public static readonly Vector3d[] CubePositions =
    [
        new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    ];

    public static readonly int[] CubeIndices =
    [
        0, 2, 1, 0, 3, 2,
        4, 5, 6, 4, 6, 7,
        0, 1, 5, 0, 5, 4,
        1, 2, 6, 1, 6, 5,
        2, 3, 7, 2, 7, 6,
        3, 0, 4, 3, 4, 7,
    ];

    public static HalfEdgeMesh Cube() => HalfEdgeMesh.FromTriangles(CubePositions, CubeIndices);

    /// <summary>Куб из шести четырёхугольных граней (без триангуляции) — для нормалей вершин.</summary>
    public static HalfEdgeMesh CubeQuads()
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles(CubePositions, []);
        mesh.AddFace([mesh.Vertices[0], mesh.Vertices[3], mesh.Vertices[2], mesh.Vertices[1]]);
        mesh.AddFace([mesh.Vertices[4], mesh.Vertices[5], mesh.Vertices[6], mesh.Vertices[7]]);
        mesh.AddFace([mesh.Vertices[0], mesh.Vertices[1], mesh.Vertices[5], mesh.Vertices[4]]);
        mesh.AddFace([mesh.Vertices[2], mesh.Vertices[3], mesh.Vertices[7], mesh.Vertices[6]]);
        mesh.AddFace([mesh.Vertices[1], mesh.Vertices[2], mesh.Vertices[6], mesh.Vertices[5]]);
        mesh.AddFace([mesh.Vertices[0], mesh.Vertices[4], mesh.Vertices[7], mesh.Vertices[3]]);
        return mesh;
    }

    public static HalfEdgeMesh Triangle(Vector3d a, Vector3d b, Vector3d c)
        => HalfEdgeMesh.FromTriangles([a, b, c], [0, 1, 2]);

    /// <summary>Поворот вокруг Z (row-vector-раскладка: строки — образы осей).</summary>
    public static Transform3d RotationZ(double radians) => new(
        Math.Cos(radians), Math.Sin(radians), 0, 0,
        -Math.Sin(radians), Math.Cos(radians), 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);

    public static Transform3d Scale(double x, double y, double z) => new(
        x, 0, 0, 0,
        0, y, 0, 0,
        0, 0, z, 0,
        0, 0, 0, 1);
}

/// <summary>Сравнение векторов с допуском по знакам после запятой.</summary>
internal static class GeometryAssert
{
    public static void VectorEqual(Vector3d expected, Vector3d actual, int precision = 9)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    /// <summary>Наличие вектора в коллекции с допуском (позиции после трансформаций дают шум последних бит).</summary>
    public static void ContainsVector(IEnumerable<Vector3d> collection, Vector3d expected, double tolerance = 1e-9)
    {
        Assert.Contains(collection, p =>
            Math.Abs(p.X - expected.X) <= tolerance
            && Math.Abs(p.Y - expected.Y) <= tolerance
            && Math.Abs(p.Z - expected.Z) <= tolerance);
    }
}
