namespace SmetaNA.Core.Geometry;

/// <summary>
/// Нормали граней и вершин меша. Все векторы единичные;
/// вырожденные элементы дают <see cref="Vector3d.Zero"/>.
/// </summary>
public static class MeshNormals
{
    /// <summary>
    /// Нормаль грани по методу Ньюэлла — устойчива к невыпуклым и слегка невыпуклым n-угольникам.
    /// Обход кольца CCW даёт нормаль по правой руке; нулевой вектор — для вырожденной грани.
    /// </summary>
    public static Vector3d ComputeFaceNormal(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);

        double x = 0, y = 0, z = 0;
        foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
        {
            Vector3d current = edge.Origin.Position;
            Vector3d next = edge.Next!.Origin.Position;
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z).Normalized();
    }

    /// <summary>Нормали всех граней в порядке <see cref="HalfEdgeMesh.Faces"/>.</summary>
    public static Vector3d[] ComputeFaceNormals(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var normals = new Vector3d[mesh.Faces.Count];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = ComputeFaceNormal(mesh.Faces[i]);
        }

        return normals;
    }

    /// <summary>
    /// Сглаженная нормаль вершины: средневзвешенная по площади нормалей инцидентных граней
    /// (сумма невырожденных нормалей Ньюэлла, длина которых пропорциональна удвоенной площади).
    /// </summary>
    public static Vector3d ComputeVertexNormal(HalfEdgeMesh mesh, MeshVertex vertex)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertex);

        var sum = Vector3d.Zero;
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            if (!ReferenceEquals(edge.Origin, vertex))
            {
                continue;
            }

            sum += UnnormalizedFaceNormal(edge.Face!);
        }

        return sum.Normalized();
    }

    /// <summary>
    /// Нормали всех вершин в порядке <see cref="HalfEdgeMesh.Vertices"/>;
    /// изолированные вершины дают <see cref="Vector3d.Zero"/>.
    /// </summary>
    public static Vector3d[] ComputeVertexNormals(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var sums = new Vector3d[mesh.Vertices.Count];
        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        foreach (MeshFace face in mesh.Faces)
        {
            Vector3d faceNormal = UnnormalizedFaceNormal(face);
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                sums[indexOf[edge.Origin]] += faceNormal;
            }
        }

        for (int i = 0; i < sums.Length; i++)
        {
            sums[i] = sums[i].Normalized();
        }

        return sums;
    }

    /// <summary>Нормали треугольников супа: по одной на каждые три индекса.</summary>
    public static Vector3d[] ComputeFaceNormals(MeshData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var normals = new Vector3d[data.Indices.Count / 3];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = ComputeTriangleNormal(
                data.Positions[data.Indices[i * 3]],
                data.Positions[data.Indices[i * 3 + 1]],
                data.Positions[data.Indices[i * 3 + 2]]);
        }

        return normals;
    }

    /// <summary>Сглаженные нормали вершин супа в порядке <see cref="MeshData.Positions"/>.</summary>
    public static Vector3d[] ComputeVertexNormals(MeshData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sums = new Vector3d[data.Positions.Count];
        for (int i = 0; i + 2 < data.Indices.Count; i += 3)
        {
            int a = data.Indices[i];
            int b = data.Indices[i + 1];
            int c = data.Indices[i + 2];
            Vector3d normal = UnnormalizedTriangleNormal(data.Positions[a], data.Positions[b], data.Positions[c]);
            sums[a] += normal;
            sums[b] += normal;
            sums[c] += normal;
        }

        for (int i = 0; i < sums.Length; i++)
        {
            sums[i] = sums[i].Normalized();
        }

        return sums;
    }

    private static Vector3d UnnormalizedFaceNormal(MeshFace face)
    {
        double x = 0, y = 0, z = 0;
        foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
        {
            Vector3d current = edge.Origin.Position;
            Vector3d next = edge.Next!.Origin.Position;
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z);
    }

    private static Vector3d ComputeTriangleNormal(Vector3d a, Vector3d b, Vector3d c)
        => UnnormalizedTriangleNormal(a, b, c).Normalized();

    private static Vector3d UnnormalizedTriangleNormal(Vector3d a, Vector3d b, Vector3d c)
        => Vector3d.Cross(b - a, c - a);
}
