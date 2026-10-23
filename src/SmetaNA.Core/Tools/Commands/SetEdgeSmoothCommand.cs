using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Команда истории: сглаживание/смягчение рёбер (Soften/Smooth). Флаги ставятся
/// на оба полуребра ребра; <see cref="IEditCommand.Undo"/> возвращает прежние флаги.
/// </summary>
public sealed class SetEdgeSmoothCommand : IEditCommand
{
    private readonly IReadOnlyList<MeshHalfEdge> _edges;
    private readonly bool _smooth;
    private readonly bool _soft;
    private List<(MeshHalfEdge HalfEdge, bool Smooth, bool Soft)> _applied = [];

    /// <param name="edges">Рёбра (по одному полуребру на ребро; пара обрабатывается автоматически).</param>
    /// <param name="smooth">Флаг сглаживания нормалей.</param>
    /// <param name="soft">Флаг мягкого (скрытого) ребра.</param>
    public SetEdgeSmoothCommand(string description, IReadOnlyList<MeshHalfEdge> edges, bool smooth, bool soft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(edges);
        Description = description;
        _edges = edges;
        _smooth = smooth;
        _soft = soft;
    }

    public string Description { get; }

    /// <summary>Рёбра, к которым применяются флаги.</summary>
    public IReadOnlyList<MeshHalfEdge> Edges => _edges;

    public void Execute()
    {
        _applied = [];
        foreach (MeshHalfEdge edge in _edges)
        {
            if (edge is null)
            {
                continue;
            }

            ApplyTo(edge);
            if (edge.Twin is { } twin)
            {
                ApplyTo(twin);
            }
        }
    }

    public void Undo()
    {
        foreach ((MeshHalfEdge halfEdge, bool smooth, bool soft) in _applied)
        {
            halfEdge.IsSmooth = smooth;
            halfEdge.IsSoft = soft;
        }
    }

    private void ApplyTo(MeshHalfEdge halfEdge)
    {
        foreach ((MeshHalfEdge existing, _, _) in _applied)
        {
            if (ReferenceEquals(existing, halfEdge))
            {
                return;
            }
        }

        _applied.Add((halfEdge, halfEdge.IsSmooth, halfEdge.IsSoft));
        halfEdge.IsSmooth = _smooth;
        halfEdge.IsSoft = _soft;
    }
}
