using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>Грань для стирания: меш-владелец и сама грань.</summary>
/// <param name="Mesh">Меш с гранью.</param>
/// <param name="Face">Стираемая грань.</param>
public readonly record struct FaceEraseTarget(HalfEdgeMesh Mesh, MeshFace Face);

/// <summary>
/// Команда истории: стирание граней (Eraser) через <see cref="MeshEditOps.EraseFaces"/>.
/// <see cref="IEditCommand.Undo"/> возвращает грани в меш с прежней идентичностью,
/// twin-связи соседних граней восстанавливаются.
/// </summary>
public sealed class EraseFacesCommand : IEditCommand
{
    private readonly IReadOnlyList<FaceEraseTarget> _targets;
    private IReadOnlyList<MeshEditOps.FaceEraseRecord> _records = [];

    public EraseFacesCommand(string description, IReadOnlyList<FaceEraseTarget> targets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(targets);
        Description = description;
        _targets = targets;
    }

    /// <summary>Стирание граней одного меша.</summary>
    public EraseFacesCommand(string description, HalfEdgeMesh mesh, IReadOnlyList<MeshFace> faces)
        : this(description, ToTargets(mesh, faces))
    {
    }

    public string Description { get; }

    /// <summary>Стираемые грани.</summary>
    public IReadOnlyList<FaceEraseTarget> Targets => _targets;

    public void Execute()
    {
        var records = new List<MeshEditOps.FaceEraseRecord>();
        foreach (var group in _targets.GroupBy(target => target.Mesh))
        {
            records.AddRange(MeshEditOps.EraseFaces(group.Key, group.Select(target => target.Face)));
        }

        _records = records;
    }

    public void Undo() => MeshEditOps.RestoreFaces(_records);

    private static IReadOnlyList<FaceEraseTarget> ToTargets(HalfEdgeMesh mesh, IReadOnlyList<MeshFace> faces)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faces);
        return [.. faces.Select(face => new FaceEraseTarget(mesh, face))];
    }
}
