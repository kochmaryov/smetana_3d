namespace SmetaNA.Core.Geometry;

/// <summary>
/// Дополнения топологии меша для инструментов рисования (задача 2.1):
/// проволочные рёбра (wire — не принадлежат ни одной грани), «надевание» грани
/// на замкнутый контур и обратные операции для команд истории (undo/redo).
/// <para>
/// Проволочное ребро — пара полуребёр «туда/обратно», связанных как twin и
/// образующих 2-цикл <see cref="MeshHalfEdge.Next"/>/<see cref="MeshHalfEdge.Prev"/>:
/// конец каждого полуребра — <c>Next.Origin</c>, поэтому рёбра участвуют в
/// каркасной отрисовке и в привязках (см. <c>InferenceSceneBuilder</c>).
/// Грань заполняет контур: его «прямые» полуребра становятся кольцом грани,
/// «обратные» остаются внешней границей. Снять грань — <see cref="DetachFace"/>,
/// удалить ребро — <see cref="RemoveWireEdge"/>.
/// </para>
/// </summary>
public sealed partial class HalfEdgeMesh
{
    /// <summary>
    /// Проволочное ребро между двумя вершинами: пара полуребёр без граней.
    /// Оба направления заносятся в регистр рёбер (повтор — ошибка).
    /// </summary>
    /// <param name="from">Начало «прямого» полуребра.</param>
    /// <param name="to">Конец «прямого» полуребра.</param>
    /// <returns>Пара полуребёр: прямое и обратное.</returns>
    public (MeshHalfEdge Forward, MeshHalfEdge Reverse) AddWireEdge(MeshVertex from, MeshVertex to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (ReferenceEquals(from, to))
        {
            throw new ArgumentException("Проволочное ребро требует двух разных вершин.", nameof(to));
        }

        if (!_vertices.Contains(from) || !_vertices.Contains(to))
        {
            throw new ArgumentException("Вершины ребра должны принадлежать мешу.");
        }

        if (_directedEdges.ContainsKey((from, to)) || _directedEdges.ContainsKey((to, from)))
        {
            throw new InvalidOperationException("Ребро между вершинами уже существует — меш не 2-манифолден.");
        }

        var forward = new MeshHalfEdge { Origin = from };
        var reverse = new MeshHalfEdge { Origin = to };
        forward.Twin = reverse;
        reverse.Twin = forward;
        forward.Next = reverse;
        forward.Prev = reverse;
        reverse.Next = forward;
        reverse.Prev = forward;

        _halfEdges.Add(forward);
        _halfEdges.Add(reverse);
        _directedEdges[(from, to)] = forward;
        _directedEdges[(to, from)] = reverse;
        from.HalfEdge ??= forward;
        to.HalfEdge ??= reverse;
        return (forward, reverse);
    }

    /// <summary>
    /// Надеть грань на замкнутый контур проволочных полуребёр: кольцо грани
    /// образуют переданные полуребра в порядке обхода (конец каждого — начало
    /// следующего), обратные полуребра становятся внешней границей.
    /// </summary>
    /// <param name="ring">Кольцо полуребёр «туда» (минимум 3, без повторов).</param>
    /// <returns>Созданная грань.</returns>
    public MeshFace FillLoop(IReadOnlyList<MeshHalfEdge> ring)
    {
        ArgumentNullException.ThrowIfNull(ring);
        if (ring.Count < 3)
        {
            throw new ArgumentException("Контур грани требует минимум трёх полуребёр.", nameof(ring));
        }

        var vertices = new HashSet<MeshVertex>();
        var used = new HashSet<MeshHalfEdge>();
        for (int i = 0; i < ring.Count; i++)
        {
            MeshHalfEdge edge = ring[i] ?? throw new ArgumentException("Контур не может содержать null.", nameof(ring));
            MeshHalfEdge twin = edge.Twin
                ?? throw new ArgumentException("Полуребро контура не имеет пары.", nameof(ring));
            if (edge.Face is not null || twin.Face is not null)
            {
                throw new ArgumentException("Полуребро контура уже принадлежит грани.", nameof(ring));
            }

            if (!used.Add(edge))
            {
                throw new ArgumentException("Контур содержит повторяющиеся полуребра.", nameof(ring));
            }

            if (!vertices.Add(edge.Origin))
            {
                throw new ArgumentException("Контур содержит повторяющиеся вершины.", nameof(ring));
            }

            MeshHalfEdge next = ring[(i + 1) % ring.Count];
            if (!ReferenceEquals(twin.Origin, next.Origin))
            {
                throw new ArgumentException("Полуребра контура идут не по порядку (конец не совпадает с началом следующего).", nameof(ring));
            }
        }

        foreach (MeshHalfEdge edge in used)
        {
            if (used.Contains(edge.Twin!))
            {
                throw new ArgumentException("Контур не может содержать оба направления одного ребра.", nameof(ring));
            }
        }

        var face = new MeshFace { HalfEdge = ring[0] };
        for (int i = 0; i < ring.Count; i++)
        {
            MeshHalfEdge edge = ring[i];
            edge.Face = face;
            edge.Next = ring[(i + 1) % ring.Count];
            edge.Prev = ring[(i + ring.Count - 1) % ring.Count];
        }

        // обратные полуребра — внешняя граница, обход в обратном порядке
        for (int i = 0; i < ring.Count; i++)
        {
            MeshHalfEdge twin = ring[i].Twin!;
            twin.Next = ring[(i + ring.Count - 1) % ring.Count].Twin;
            twin.Prev = ring[(i + 1) % ring.Count].Twin;
        }

        _faces.Add(face);
        return face;
    }

    /// <summary>
    /// Снять грань с контура: полуребра снова становятся проволочными (2-циклы
    /// пар восстанавливаются). Грань, делящая ребро с другой гранью, не
    /// поддерживается — такие рёбра остаются топологией граней.
    /// </summary>
    public void DetachFace(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        if (!_faces.Remove(face))
        {
            throw new ArgumentException("Грань не принадлежит мешу.", nameof(face));
        }

        var ring = new List<MeshHalfEdge>();
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            ring.Add(edge);
            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);

        foreach (MeshHalfEdge current in ring)
        {
            if (current.Twin is { Face: not null })
            {
                _faces.Add(face);
                throw new InvalidOperationException("Грань делит ребро с другой гранью — снятие не поддерживается.");
            }
        }

        foreach (MeshHalfEdge current in ring)
        {
            MeshHalfEdge twin = current.Twin!;
            current.Face = null;
            current.Next = twin;
            current.Prev = twin;
            twin.Next = current;
            twin.Prev = current;
        }
    }

    /// <summary>
    /// Удалить проволочное ребро (пару полуребёр). Ребро, принадлежащее хотя бы
    /// одной грани, удалить нельзя.
    /// </summary>
    public void RemoveWireEdge(MeshHalfEdge forward)
    {
        ArgumentNullException.ThrowIfNull(forward);
        MeshHalfEdge reverse = forward.Twin
            ?? throw new ArgumentException("Полуребро не имеет пары — проволочного ребра нет.", nameof(forward));
        if (forward.Face is not null || reverse.Face is not null)
        {
            throw new ArgumentException("Ребро принадлежит грани — удаление проволочного ребра невозможно.", nameof(forward));
        }

        if (!ReferenceEquals(forward.Next, reverse) || !ReferenceEquals(reverse.Next, forward))
        {
            throw new ArgumentException("Полуребра не образуют проволочную пару.", nameof(forward));
        }

        if (!_halfEdges.Remove(forward) || !_halfEdges.Remove(reverse))
        {
            throw new ArgumentException("Полуребро не принадлежит мешу.", nameof(forward));
        }

        if (_directedEdges.TryGetValue((forward.Origin, reverse.Origin), out MeshHalfEdge? registered)
            && ReferenceEquals(registered, forward))
        {
            _directedEdges.Remove((forward.Origin, reverse.Origin));
        }

        if (_directedEdges.TryGetValue((reverse.Origin, forward.Origin), out registered)
            && ReferenceEquals(registered, reverse))
        {
            _directedEdges.Remove((reverse.Origin, forward.Origin));
        }

        RefreshVertexHalfEdge(forward.Origin);
        RefreshVertexHalfEdge(reverse.Origin);
    }

    /// <summary>Исходящее полуребро вершины после удалений (или <c>null</c>).</summary>
    private void RefreshVertexHalfEdge(MeshVertex vertex)
    {
        MeshHalfEdge? replacement = null;
        foreach (MeshHalfEdge edge in _halfEdges)
        {
            if (ReferenceEquals(edge.Origin, vertex))
            {
                replacement = edge;
                break;
            }
        }

        vertex.HalfEdge = replacement;
    }
}
