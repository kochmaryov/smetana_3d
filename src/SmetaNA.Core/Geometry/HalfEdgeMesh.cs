using SmetaNA.Core.Materials;

namespace SmetaNA.Core.Geometry;

/// <summary>
/// Топология меша в терминах полумногоугольников (mesh-first, спека §4).
/// Контракт для фаз 1–2: построение граней и twin-пары; алгоритмы (нормали, батчинг) — задача 1.1.
/// </summary>
public sealed class HalfEdgeMesh
{
    private readonly List<MeshVertex> _vertices = [];
    private readonly List<MeshHalfEdge> _halfEdges = [];
    private readonly List<MeshFace> _faces = [];
    private readonly Dictionary<(MeshVertex From, MeshVertex To), MeshHalfEdge> _directedEdges = [];

    public IReadOnlyList<MeshVertex> Vertices => _vertices;
    public IReadOnlyList<MeshHalfEdge> HalfEdges => _halfEdges;
    public IReadOnlyList<MeshFace> Faces => _faces;

    /// <summary>Замкнутость (watertight): у каждого полуребра есть пара, границ нет.</summary>
    public bool IsWatertight => _halfEdges.Count > 0 && _halfEdges.All(e => e.Twin is not null);

    /// <summary>
    /// Палитра материалов меша: индексы <see cref="MeshFace.MaterialIndex"/> и
    /// <see cref="DefaultMaterialIndex"/> указывают в неё. По умолчанию — стандартная
    /// палитра (<see cref="MaterialPalette.CreateDefault"/>), порядок её стабилен.
    /// </summary>
    public MaterialTable Materials { get; set; } = MaterialPalette.CreateDefault();

    /// <summary>
    /// Материал меша по умолчанию («как у объекта») для граней без явного индекса;
    /// -1 — базовый цвет без материала.
    /// </summary>
    public int DefaultMaterialIndex { get; set; } = -1;

    public MeshVertex AddVertex(Vector3d position)
    {
        var vertex = new MeshVertex { Position = position };
        _vertices.Add(vertex);
        return vertex;
    }

    /// <summary>
    /// Грань по кольцу вершин (минимум 3). Обход CCW — нормаль по правой руке.
    /// Соседние грани с общим ребром автоматически связываются в twin-пару.
    /// </summary>
    public MeshFace AddFace(IReadOnlyList<MeshVertex> ring)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Count < 3)
        {
            throw new ArgumentException("Грань требует минимум трёх вершин.", nameof(ring));
        }

        var edges = new MeshHalfEdge[ring.Count];
        for (int i = 0; i < ring.Count; i++)
        {
            MeshVertex origin = ring[i];
            MeshVertex destination = ring[(i + 1) % ring.Count];
            if (ReferenceEquals(origin, destination))
            {
                throw new ArgumentException("Соседние вершины грани совпадают.", nameof(ring));
            }

            edges[i] = new MeshHalfEdge { Origin = origin };
        }

        for (int i = 0; i < ring.Count; i++)
        {
            edges[i].Next = edges[(i + 1) % ring.Count];
            edges[i].Prev = edges[(i + ring.Count - 1) % ring.Count];
        }

        var face = new MeshFace { HalfEdge = edges[0] };
        foreach (MeshHalfEdge edge in edges)
        {
            edge.Face = face;
            if (edge.Origin.HalfEdge is null)
            {
                edge.Origin.HalfEdge = edge;
            }
        }

        foreach (MeshHalfEdge edge in edges)
        {
            var key = (edge.Origin, edge.Next!.Origin);
            if (_directedEdges.ContainsKey(key))
            {
                throw new InvalidOperationException("Повторяющееся направленное ребро — меш невалиден.");
            }

            if (_directedEdges.TryGetValue((key.Item2, key.Item1), out MeshHalfEdge? reverse))
            {
                if (reverse.Twin is not null)
                {
                    throw new InvalidOperationException("Ребро уже принадлежит двум граням — меш не 2-манифолден.");
                }

                edge.Twin = reverse;
                reverse.Twin = edge;
            }

            _directedEdges[key] = edge;
        }

        _halfEdges.AddRange(edges);
        _faces.Add(face);
        return face;
    }

    /// <summary>
    /// Построение из треугольного супа: вершины общие по индексу
    /// (сварка дубликатов позиций — задача импорта форматов).
    /// </summary>
    public static HalfEdgeMesh FromTriangles(IReadOnlyList<Vector3d> positions, IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(indices));
        }

        var mesh = new HalfEdgeMesh();
        var vertices = new MeshVertex[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            vertices[i] = mesh.AddVertex(positions[i]);
        }

        for (int i = 0; i < indices.Count; i += 3)
        {
            mesh.AddFace([vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]]);
        }

        return mesh;
    }

    /// <summary>Кольцо вершин грани в порядке обхода CCW.</summary>
    public IReadOnlyList<MeshVertex> GetRing(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        var ring = new List<MeshVertex>();
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            ring.Add(edge.Origin);
            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);
        return ring;
    }

    /// <summary>Грани, соседние с данной по общему ребру (twin-пара).</summary>
    public IReadOnlyList<MeshFace> GetAdjacentFaces(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        var adjacent = new List<MeshFace>();
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            if (edge.Twin?.Face is { } neighbour && !ReferenceEquals(neighbour, face) && !adjacent.Contains(neighbour))
            {
                adjacent.Add(neighbour);
            }

            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);
        return adjacent;
    }

    /// <summary>
    /// Удалить грань из меша (стирание): полуребра грани убираются из обхода,
    /// twin-связи соседних граней разрываются. Грань и её полуребра сохраняются
    /// как есть — <see cref="RestoreFace"/> вернёт их с прежней идентичностью (undo).
    /// </summary>
    public bool RemoveFace(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (!_faces.Remove(face))
        {
            return false;
        }

        List<MeshHalfEdge> edges = RingEdges(face);
        var removed = new HashSet<MeshHalfEdge>(edges);
        foreach (MeshHalfEdge edge in edges)
        {
            var key = (edge.Origin, edge.Next!.Origin);
            if (_directedEdges.TryGetValue(key, out MeshHalfEdge? mapped) && ReferenceEquals(mapped, edge))
            {
                _directedEdges.Remove(key);
            }

            if (edge.Twin is { } twin && ReferenceEquals(twin.Twin, edge))
            {
                twin.Twin = null;
            }
        }

        foreach (MeshHalfEdge edge in edges)
        {
            _halfEdges.Remove(edge);
        }

        foreach (MeshHalfEdge edge in edges)
        {
            MeshVertex vertex = edge.Origin;
            if (vertex.HalfEdge is not null && removed.Contains(vertex.HalfEdge))
            {
                vertex.HalfEdge = FindOutgoing(vertex);
            }
        }

        return true;
    }

    /// <summary>
    /// Вернуть в меш ранее удалённую грань с прежними полуребрами и twin-связями
    /// (обратная операция к <see cref="RemoveFace"/>).
    /// </summary>
    public void RestoreFace(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (_faces.Contains(face))
        {
            throw new InvalidOperationException("Грань уже находится в меше.");
        }

        List<MeshHalfEdge> edges = RingEdges(face);
        foreach (MeshHalfEdge edge in edges)
        {
            var key = (edge.Origin, edge.Next!.Origin);
            if (_directedEdges.ContainsKey(key))
            {
                throw new InvalidOperationException("Повторяющееся направленное ребро — меш невалиден.");
            }
        }

        foreach (MeshHalfEdge edge in edges)
        {
            _directedEdges[(edge.Origin, edge.Next!.Origin)] = edge;
        }

        foreach (MeshHalfEdge edge in edges)
        {
            if (edge.Twin is { } twin)
            {
                if (twin.Twin is null)
                {
                    twin.Twin = edge;
                }
            }
            else if (_directedEdges.TryGetValue((edge.Next!.Origin, edge.Origin), out MeshHalfEdge? reverse))
            {
                edge.Twin = reverse;
                reverse.Twin = edge;
            }
        }

        _halfEdges.AddRange(edges);
        _faces.Add(face);

        foreach (MeshHalfEdge edge in edges)
        {
            if (edge.Origin.HalfEdge is null)
            {
                edge.Origin.HalfEdge = edge;
            }
        }
    }

    /// <summary>
    /// Удалить изолированную вершину (без инцидентных рёбер). Позиция сохраняется
    /// в самом объекте вершины — <see cref="RestoreVertex"/> вернёт её в меш.
    /// </summary>
    public bool RemoveVertex(MeshVertex vertex)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        foreach (MeshHalfEdge edge in _halfEdges)
        {
            if (ReferenceEquals(edge.Origin, vertex))
            {
                throw new InvalidOperationException("Вершина инцидентна ребру меша — сначала удалите связанные грани.");
            }
        }

        return _vertices.Remove(vertex);
    }

    /// <summary>Вернуть в меш ранее удалённую вершину (обратная операция к <see cref="RemoveVertex"/>).</summary>
    public void RestoreVertex(MeshVertex vertex)
    {
        ArgumentNullException.ThrowIfNull(vertex);
        if (_vertices.Contains(vertex))
        {
            throw new InvalidOperationException("Вершина уже находится в меше.");
        }

        _vertices.Add(vertex);
    }

    private List<MeshHalfEdge> RingEdges(MeshFace face)
    {
        var edges = new List<MeshHalfEdge>();
        MeshHalfEdge start = face.HalfEdge;
        MeshHalfEdge edge = start;
        do
        {
            edges.Add(edge);
            edge = edge.Next!;
        }
        while (edge != start);
        return edges;
    }

    private MeshHalfEdge? FindOutgoing(MeshVertex vertex)
    {
        foreach (MeshHalfEdge edge in _halfEdges)
        {
            if (ReferenceEquals(edge.Origin, vertex))
            {
                return edge;
            }
        }

        return null;
    }

    /// <summary>
    /// Разложение в треугольный суп (веерная триангуляция n-угольных граней) —
    /// обмен с форматами (STL/OBJ) и CSG-бэкендом (Manifold).
    /// </summary>
    public MeshData ToTriangles()
    {
        var positions = new Vector3d[_vertices.Count];
        var indexOf = new Dictionary<MeshVertex, int>(_vertices.Count);
        for (int i = 0; i < _vertices.Count; i++)
        {
            positions[i] = _vertices[i].Position;
            indexOf[_vertices[i]] = i;
        }

        var indices = new List<int>(_faces.Count * 3);
        foreach (MeshFace face in _faces)
        {
            var ring = new List<MeshVertex>();
            MeshHalfEdge edge = face.HalfEdge;
            do
            {
                ring.Add(edge.Origin);
                edge = edge.Next!;
            }
            while (edge != face.HalfEdge);

            for (int i = 1; i + 1 < ring.Count; i++)
            {
                indices.Add(indexOf[ring[0]]);
                indices.Add(indexOf[ring[i]]);
                indices.Add(indexOf[ring[i + 1]]);
            }
        }

        return new MeshData(positions, [.. indices]);
    }
}

/// <summary>Треугольный суп (позиции + индексы): обменное представление с форматами и CSG.</summary>
public sealed record MeshData(IReadOnlyList<Vector3d> Positions, IReadOnlyList<int> Indices);

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

    /// <summary>Через ребро сглаживаются нормали соседних граней (Soften/Smooth).</summary>
    public bool IsSmooth { get; set; }

    /// <summary>Ребро не рисуется как жёсткое ребро (Soften/Smooth).</summary>
    public bool IsSoft { get; set; }
}

public sealed class MeshFace
{
    public required MeshHalfEdge HalfEdge { get; init; }
    public int MaterialIndex { get; set; } = -1;
}
