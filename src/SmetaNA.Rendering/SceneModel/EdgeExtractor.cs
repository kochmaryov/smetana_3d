using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Извлечение рёбер для каркасного режима: пары индексов вершин, без дублей.
/// </summary>
public static class EdgeExtractor
{
    /// <summary>
    /// Рёбра топологии полумногоугольников — только настоящие рёбра граней,
    /// без диагоналей веерной триангуляции. Индексы совпадают с порядком вершин
    /// <see cref="HalfEdgeMesh.ToTriangles"/>.
    /// </summary>
    public static int[] FromHalfEdgeMesh(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        var edges = new List<int>(mesh.HalfEdges.Count);
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            int from = indexOf[edge.Origin];
            int to = indexOf[edge.Next!.Origin];

            // У каждого внутреннего ребра два полуребра — оставляем одно (по порядку индексов);
            // граничные (без пары) оставляем всегда.
            if (edge.Twin is null || from < to)
            {
                edges.Add(from);
                edges.Add(to);
            }
        }

        return [.. edges];
    }

    /// <summary>Рёбра треугольного супа с дедупликацией (включая диагонали).</summary>
    public static int[] FromTriangles(MeshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(data));
        }

        var seen = new HashSet<(int From, int To)>();
        var edges = new List<int>(data.Indices.Count);
        for (int i = 0; i < data.Indices.Count; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = data.Indices[i + k];
                int b = data.Indices[i + ((k + 1) % 3)];
                var key = a < b ? (a, b) : (b, a);
                if (seen.Add(key))
                {
                    edges.Add(a);
                    edges.Add(b);
                }
            }
        }

        return [.. edges];
    }
}
