using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering.Tests;

/// <summary>Готовая тестовая геометрия: куб, квад, треугольник.</summary>
internal static class TestMeshes
{
    /// <summary>Куб со стороной <paramref name="size"/> (мм), центр — в начале координат.</summary>
    public static HalfEdgeMesh Box(double size = 100)
    {
        double h = size * 0.5;
        var mesh = new HalfEdgeMesh();
        var v = new MeshVertex[8];
        for (int i = 0; i < 8; i++)
        {
            v[i] = mesh.AddVertex(new Vector3d(
                (i & 1) == 0 ? -h : h,
                (i & 2) == 0 ? -h : h,
                (i & 4) == 0 ? -h : h));
        }

        mesh.AddFace([v[0], v[2], v[3], v[1]]); // низ
        mesh.AddFace([v[4], v[5], v[7], v[6]]); // верх
        mesh.AddFace([v[0], v[1], v[5], v[4]]); // перед (−Y)
        mesh.AddFace([v[2], v[6], v[7], v[3]]); // зад (+Y)
        mesh.AddFace([v[0], v[4], v[6], v[2]]); // лево (−X)
        mesh.AddFace([v[1], v[3], v[7], v[5]]); // право (+X)
        return mesh;
    }

    /// <summary>Квад 100×100 мм в плоскости Z=0 (две грани-треугольника не нужны — квад целиком).</summary>
    public static HalfEdgeMesh Quad(double size = 100)
    {
        double h = size * 0.5;
        var mesh = new HalfEdgeMesh();
        MeshVertex a = mesh.AddVertex(new Vector3d(-h, -h, 0));
        MeshVertex b = mesh.AddVertex(new Vector3d(h, -h, 0));
        MeshVertex c = mesh.AddVertex(new Vector3d(h, h, 0));
        MeshVertex d = mesh.AddVertex(new Vector3d(-h, h, 0));
        mesh.AddFace([a, b, c, d]);
        return mesh;
    }
}
