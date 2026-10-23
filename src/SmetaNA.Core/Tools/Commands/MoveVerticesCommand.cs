using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Команда истории: перенос вершин меша по плану <see cref="VertexMove"/>
/// (используется push/pull со склеиванием). <see cref="IEditCommand.Undo"/>
/// возвращает исходные позиции.
/// </summary>
public sealed class MoveVerticesCommand : IEditCommand
{
    public MoveVerticesCommand(string description, IReadOnlyList<VertexMove> moves)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(moves);
        Description = description;
        Moves = moves;
    }

    public string Description { get; }

    /// <summary>План переноса (позиции до/после).</summary>
    public IReadOnlyList<VertexMove> Moves { get; }

    public void Execute() => MeshEditOps.Apply(Moves);

    public void Undo() => MeshEditOps.Revert(Moves);
}
