using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Materials;

/// <summary>
/// Назначение материала грани: снимок индекса до/после (отмена возвращает
/// <see cref="From"/>); -1 — материал снят (грань наследует материал объекта).
/// </summary>
/// <param name="Face">Грань меша.</param>
/// <param name="From">Индекс материала до смены.</param>
/// <param name="To">Индекс материала после смены.</param>
public readonly record struct FaceMaterialAssignment(MeshFace Face, int From, int To);

/// <summary>
/// Команда истории: назначение/смена/снятие материалов граней одной операцией
/// (Paint Bucket). <see cref="IEditCommand.Undo"/> возвращает исходные индексы.
/// </summary>
public sealed class SetFaceMaterialsCommand : IEditCommand
{
    private readonly IReadOnlyList<FaceMaterialAssignment> _assignments;

    public SetFaceMaterialsCommand(string description, IReadOnlyList<FaceMaterialAssignment> assignments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(assignments);
        Description = description;
        _assignments = assignments;
    }

    public string Description { get; }

    /// <summary>Назначения материалов граней (позиции до/после).</summary>
    public IReadOnlyList<FaceMaterialAssignment> Assignments => _assignments;

    public void Execute()
    {
        foreach (FaceMaterialAssignment assignment in _assignments)
        {
            assignment.Face.MaterialIndex = assignment.To;
        }
    }

    public void Undo()
    {
        for (int i = _assignments.Count - 1; i >= 0; i--)
        {
            _assignments[i].Face.MaterialIndex = _assignments[i].From;
        }
    }
}

/// <summary>
/// Команда истории: материал меша по умолчанию («материал объекта») — база для
/// граней без явного индекса. Исходный индекс запоминается при первом выполнении.
/// </summary>
public sealed class SetMeshMaterialCommand : IEditCommand
{
    private readonly HalfEdgeMesh _mesh;
    private readonly int _newIndex;
    private int _oldIndex = -1;

    /// <param name="materialIndex">Индекс материала в палитре меша или -1 (снять).</param>
    public SetMeshMaterialCommand(HalfEdgeMesh mesh, int materialIndex)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _mesh = mesh;
        _newIndex = materialIndex;
    }

    public string Description => "Материал объекта";

    public void Execute()
    {
        _oldIndex = _mesh.DefaultMaterialIndex;
        _mesh.DefaultMaterialIndex = _newIndex;
    }

    public void Undo() => _mesh.DefaultMaterialIndex = _oldIndex;
}
