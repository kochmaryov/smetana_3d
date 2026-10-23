namespace SmetaNA.Rendering;

/// <summary>Диапазон одного объекта в общих GPU-буферах.</summary>
public sealed record ObjectRange(
    RenderObject Object,
    int VertexOffset,
    int VertexCount,
    int TriangleFirst,
    int TriangleCount,
    int EdgeFirst,
    int EdgeCount);

/// <summary>Готовые к загрузке в GPU батчи: вершины, треугольники, рёбра, диапазоны объектов.</summary>
public sealed class RenderBatch
{
    public required float[] Vertices { get; init; }

    public required uint[] TriangleIndices { get; init; }

    public required uint[] EdgeIndices { get; init; }

    public required IReadOnlyList<ObjectRange> Ranges { get; init; }

    public int VertexCount => Vertices.Length / 3;

    public int TriangleCount => TriangleIndices.Length / 3;

    public int EdgeCount => EdgeIndices.Length / 2;
}

/// <summary>
/// Батчинг объектов сцены в общие буферы (задача 1.5): одна вершина на точку меша,
/// индексы треугольников и рёбер со смещениями, по диапазону на объект — этого
/// достаточно для пообъектного выделения без лишних draw-вызовов на треугольник.
/// </summary>
public static class SceneBatcher
{
    public static RenderBatch Build(IReadOnlyList<RenderObject> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);

        int vertexTotal = 0;
        int triangleIndexTotal = 0;
        int edgeIndexTotal = 0;
        foreach (RenderObject obj in objects)
        {
            vertexTotal += obj.Triangles.Positions.Count;
            triangleIndexTotal += obj.Triangles.Indices.Count;
            edgeIndexTotal += obj.EdgeIndices.Length;
        }

        var vertices = new float[vertexTotal * 3];
        var triangleIndices = new uint[triangleIndexTotal];
        var edgeIndices = new uint[edgeIndexTotal];
        var ranges = new List<ObjectRange>(objects.Count);

        int vertexBase = 0;
        int triangleCursor = 0;
        int edgeCursor = 0;
        foreach (RenderObject obj in objects)
        {
            IReadOnlyList<SmetaNA.Core.Geometry.Vector3d> positions = obj.Triangles.Positions;
            for (int i = 0; i < positions.Count; i++)
            {
                int offset = ((vertexBase + i) * 3) + 0;
                vertices[offset] = (float)positions[i].X;
                vertices[offset + 1] = (float)positions[i].Y;
                vertices[offset + 2] = (float)positions[i].Z;
            }

            IReadOnlyList<int> indices = obj.Triangles.Indices;
            for (int i = 0; i < indices.Count; i++)
            {
                triangleIndices[triangleCursor + i] = (uint)(vertexBase + indices[i]);
            }

            for (int i = 0; i < obj.EdgeIndices.Length; i++)
            {
                edgeIndices[edgeCursor + i] = (uint)(vertexBase + obj.EdgeIndices[i]);
            }

            ranges.Add(new ObjectRange(
                obj,
                vertexBase,
                positions.Count,
                triangleCursor,
                indices.Count,
                edgeCursor,
                obj.EdgeIndices.Length));

            vertexBase += positions.Count;
            triangleCursor += indices.Count;
            edgeCursor += obj.EdgeIndices.Length;
        }

        return new RenderBatch
        {
            Vertices = vertices,
            TriangleIndices = triangleIndices,
            EdgeIndices = edgeIndices,
            Ranges = ranges,
        };
    }
}
