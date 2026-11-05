using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Команда истории: выдавливание грани со свободной границей (<see cref="MeshEditOps.IsBoundaryFace"/>).
/// <para>
/// Из плоской грани строится призма: вторая крышка и боковые стенки. Исходная
/// грань остаётся в меше (её идентичность сохраняется для выбора и предпросмотра):
/// при положительном смещении она переносится на дальний конец, при отрицательном
/// остаётся основанием. Свободная проволочная граница исходной фигуры снимается
/// (<see cref="HalfEdgeMesh.DetachBoundary"/>) — её место занимают стенки.
/// </para>
/// <para>
/// Результат замкнут (watertight): каждая стенка спарена и с исходной гранью,
/// и со второй крышкой. <see cref="Undo"/> удаляет крышку и стенки, возвращает
/// позиции вершин и проволочную границу.
/// </para>
/// </summary>
public sealed class ExtrudeFaceCommand : IEditCommand
{
    private readonly HalfEdgeMesh _mesh;
    private readonly MeshFace _face;
    private readonly double _distance;
    private readonly IReadOnlyList<MeshVertex> _glueScope;
    private IReadOnlyList<MeshVertex> _ring = [];
    private IReadOnlyList<VertexMove> _moves = [];
    private List<MeshVertex>? _capRing;
    private List<MeshFace>? _newFaces;
    private IReadOnlyList<HalfEdgeMesh.BoundaryEdgePair> _boundary = [];
    private bool _executed;

    public ExtrudeFaceCommand(
        HalfEdgeMesh mesh,
        MeshFace face,
        double distance,
        IEnumerable<MeshVertex>? glueScope = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(face);
        if (!double.IsFinite(distance) || Math.Abs(distance) < 1e-9)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Смещение должно быть конечным и отличным от нуля.");
        }

        _mesh = mesh;
        _face = face;
        _distance = distance;
        _glueScope = glueScope is null ? [] : [.. glueScope];
    }

    public string Description =>
        string.Create(CultureInfo.InvariantCulture, $"Push/Pull {_distance:0.###} mm");

    /// <summary>Смещение вдоль нормали грани, мм (со знаком).</summary>
    public double Distance => _distance;

    /// <summary>Вершины второй крышки (создаются при первом выполнении).</summary>
    public IReadOnlyList<MeshVertex> CapVertices => _capRing ?? [];

    /// <summary>Новые грани: вторая крышка и стенки.</summary>
    public IReadOnlyList<MeshFace> NewFaces => _newFaces ?? [];

    public void Execute()
    {
        if (!_executed)
        {
            Build();
            _executed = true;
            return;
        }

        RestoreCreated();
    }

    public void Undo()
    {
        if (!_executed)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        MeshEditOps.EraseFaces(_mesh, _newFaces!);
        foreach (MeshVertex vertex in _capRing!)
        {
            _mesh.RemoveVertex(vertex);
        }

        MeshEditOps.Revert(_moves);
        _mesh.RestoreBoundary(_boundary);
    }

    private void Build()
    {
        if (!MeshEditOps.IsBoundaryFace(_face))
        {
            throw new InvalidOperationException("Выдавливание со стенками требует грани со свободной границей.");
        }

        _ring = _mesh.GetRing(_face);
        if (_ring.Count < 3)
        {
            throw new InvalidOperationException("Выдавливание требует грань минимум с тремя вершинами.");
        }

        Vector3d normal = MeshNormals.ComputeFaceNormal(_face).Normalized();
        if (normal.LengthSquared() <= 0)
        {
            throw new InvalidOperationException("Нормаль грани не определена (вырожденная грань).");
        }

        Vector3d delta = normal * _distance;
        var positions = new List<Vector3d>(_ring.Count);
        foreach (MeshVertex vertex in _ring)
        {
            positions.Add(vertex.Position);
        }

        if (_distance > 0)
        {
            // исходная грань уходит на дальний конец и становится верхней крышкой;
            // основание строится дубликатами вершин в исходных позициях
            _moves = MeshEditOps.PlanMove(_ring, _glueScope, delta);
            MeshEditOps.Apply(_moves);
        }

        _capRing = new List<MeshVertex>(_ring.Count);
        for (int i = 0; i < _ring.Count; i++)
        {
            Vector3d position = _distance > 0 ? positions[i] : positions[i] + delta;
            _capRing.Add(_mesh.AddVertex(position));
        }

        _boundary = _mesh.DetachBoundary(_face);
        _newFaces = BuildFaces();
    }

    private void RestoreCreated()
    {
        foreach (MeshVertex vertex in _capRing!)
        {
            _mesh.RestoreVertex(vertex);
        }

        MeshEditOps.Apply(_moves);
        _boundary = _mesh.DetachBoundary(_face);
        foreach (MeshFace face in _newFaces!)
        {
            _mesh.RestoreFace(face);
        }
    }

    private List<MeshFace> BuildFaces()
    {
        int count = _ring.Count;
        int material = _face.MaterialIndex;
        var faces = new List<MeshFace>(count + 2);
        List<MeshVertex> cap = _capRing!;

        var reversedCap = new List<MeshVertex>(count);
        for (int i = count - 1; i >= 0; i--)
        {
            reversedCap.Add(cap[i]);
        }

        faces.Add(AddFace(reversedCap, material));
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            IReadOnlyList<MeshVertex> side = _distance > 0
                ? [cap[i], cap[next], _ring[next], _ring[i]]
                : [_ring[next], _ring[i], cap[i], cap[next]];
            faces.Add(AddFace(side, material));
        }

        return faces;
    }

    private MeshFace AddFace(IReadOnlyList<MeshVertex> ring, int material)
    {
        MeshFace face = _mesh.AddFace(ring);
        face.MaterialIndex = material;
        return face;
    }
}
