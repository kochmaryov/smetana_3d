namespace SmetaNA.Core.Geometry;

/// <summary>
/// Трансформирование мешей аффинным преобразованием (<see cref="Transform3d"/>).
/// Нормали при необходимости пересчитываются из преобразованной геометрии —
/// это корректно и для неоднородного масштаба (в отличие от TransformVector).
/// </summary>
public static class MeshTransforms
{
    /// <summary>Копия меша с преобразованными позициями вершин; топология и материалы сохраняются.</summary>
    public static HalfEdgeMesh Transformed(HalfEdgeMesh mesh, Transform3d transform)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var copy = new HalfEdgeMesh
        {
            // палитра общая: индексы материалов копии ссылаются на те же материалы
            Materials = mesh.Materials,
            DefaultMaterialIndex = mesh.DefaultMaterialIndex,
        };
        var mapping = new Dictionary<MeshVertex, MeshVertex>(mesh.Vertices.Count);
        foreach (MeshVertex vertex in mesh.Vertices)
        {
            mapping[vertex] = copy.AddVertex(transform.TransformPoint(vertex.Position));
        }

        foreach (MeshFace face in mesh.Faces)
        {
            var ring = new List<MeshVertex>();
            var sourceEdges = new List<MeshHalfEdge>();
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                ring.Add(mapping[edge.Origin]);
                sourceEdges.Add(edge);
            }

            MeshFace created = copy.AddFace(ring);
            created.MaterialIndex = face.MaterialIndex;

            MeshHalfEdge createdEdge = created.HalfEdge;
            foreach (MeshHalfEdge sourceEdge in sourceEdges)
            {
                createdEdge.IsSmooth = sourceEdge.IsSmooth;
                createdEdge.IsSoft = sourceEdge.IsSoft;
                createdEdge = createdEdge.Next!;
            }
        }

        return copy;
    }

    /// <summary>Преобразование меша на месте (только позиции вершин).</summary>
    public static void TransformInPlace(HalfEdgeMesh mesh, Transform3d transform)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        foreach (MeshVertex vertex in mesh.Vertices)
        {
            vertex.Position = transform.TransformPoint(vertex.Position);
        }
    }

    /// <summary>Копия треугольного супа с преобразованными позициями; индексы без изменений.</summary>
    public static MeshData Transformed(MeshData data, Transform3d transform)
    {
        ArgumentNullException.ThrowIfNull(data);

        var positions = new Vector3d[data.Positions.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = transform.TransformPoint(data.Positions[i]);
        }

        return new MeshData(positions, [.. data.Indices]);
    }
}
