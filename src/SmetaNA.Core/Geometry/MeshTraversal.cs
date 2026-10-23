namespace SmetaNA.Core.Geometry;

/// <summary>Обход топологии полумногоугольников: кольца граней и смежные структуры.</summary>
internal static class MeshTraversal
{
    /// <summary>Полуребра грани в порядке кольца (один проход).</summary>
    public static IEnumerable<MeshHalfEdge> EnumerateRing(MeshFace face)
    {
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            yield return edge;
            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);
    }
}
