namespace SmetaNA.Core.Geometry;

/// <summary>
/// Топология меша в терминах полумногоугольников (mesh-first, спека §4).
/// Навигационная структура данных — контракт для фаз 1–2; построение и проверки (watertight) — задачи 1.1 и 4.4.
/// </summary>
public sealed class HalfEdgeMesh
{
    private readonly List<MeshVertex> _vertices = [];
    private readonly List<MeshHalfEdge> _halfEdges = [];
    private readonly List<MeshFace> _faces = [];

    public IReadOnlyList<MeshVertex> Vertices => _vertices;
    public IReadOnlyList<MeshHalfEdge> HalfEdges => _halfEdges;
    public IReadOnlyList<MeshFace> Faces => _faces;

    public MeshVertex AddVertex(Vector3d position)
    {
        var vertex = new MeshVertex { Position = position };
        _vertices.Add(vertex);
        return vertex;
    }
}

public sealed class MeshVertex
{
    public required Vector3d Position { get; set; }

    /// <summary>Одно исходящее полуребро; null для изолированной вершины.</summary>
    public MeshHalfEdge? HalfEdge { get; set; }
}

public sealed class MeshHalfEdge
{
    public required MeshVertex Origin { get; init; }

    /// <summary>Грань-владелец; null для границы (незамкнутый контур).</summary>
    public MeshFace? Face { get; set; }

    public MeshHalfEdge? Next { get; set; }
    public MeshHalfEdge? Prev { get; set; }

    /// <summary>Пара (обратное полуребро); null для границы — по ней считается watertight.</summary>
    public MeshHalfEdge? Twin { get; set; }
}

public sealed class MeshFace
{
    public required MeshHalfEdge HalfEdge { get; init; }
    public int MaterialIndex { get; set; } = -1;
}
