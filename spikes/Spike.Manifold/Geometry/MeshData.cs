namespace Spike.Manifold;

/// <summary>Булева операция (соответствует ManifoldOpType из C API).</summary>
public enum BooleanOp
{
    Union = 0,
    Difference = 1,
    Intersection = 2,
}

/// <summary>
/// Управляемое представление меша: вершины (xyz, double) + треугольники (индексы ulong).
/// Промежуточное звено между HalfEdgeMesh и Manifold MeshGL64.
/// </summary>
public sealed class MeshData
{
    public MeshData(double[] vertices, ulong[] triangles)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(triangles);
        if (vertices.Length == 0 || vertices.Length % 3 != 0)
            throw new ArgumentException("vertices must be a non-empty multiple of 3 (xyz per vertex)", nameof(vertices));
        if (triangles.Length == 0 || triangles.Length % 3 != 0)
            throw new ArgumentException("triangles must be a non-empty multiple of 3 (indices per triangle)", nameof(triangles));
        foreach (ulong idx in triangles)
        {
            if (idx >= (ulong)(vertices.Length / 3))
                throw new ArgumentException($"triangle index {idx} out of bounds ({vertices.Length / 3} vertices)", nameof(triangles));
        }

        Vertices = vertices;
        Triangles = triangles;
    }

    /// <summary>Координаты вершин: [x0,y0,z0, x1,y1,z1, ...].</summary>
    public double[] Vertices { get; }

    /// <summary>Индексы треугольников: [a0,b0,c0, a1,b1,c1, ...].</summary>
    public ulong[] Triangles { get; }

    public int VertexCount => Vertices.Length / 3;

    public int TriangleCount => Triangles.Length / 3;
}
