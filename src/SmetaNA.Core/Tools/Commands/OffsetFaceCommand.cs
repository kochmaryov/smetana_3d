using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Команда истории: Offset грани — концентрический контур в плоскости грани
/// (<see cref="OffsetMath"/>) и расщепление исходной грани на кольцо квадов
/// и внутреннюю грань. Положительное расстояние — инсет, отрицательное — аутсет.
/// Материал исходной грани наследуют все новые грани; <see cref="IEditCommand.Undo"/>
/// убирает новые грани и вершины и возвращает исходную грань с прежней идентичностью.
/// </summary>
public sealed class OffsetFaceCommand : IEditCommand
{
    /// <summary>Нулевое смещение не создаёт команду.</summary>
    public const double MinCommitDistance = 1e-9;

    private readonly HalfEdgeMesh _mesh;
    private readonly MeshFace _face;
    private readonly double _distance;
    private IReadOnlyList<MeshEditOps.FaceEraseRecord> _erased = [];
    private List<MeshVertex>? _newVertices;
    private List<IReadOnlyList<MeshVertex>>? _newRings;
    private List<MeshFace>? _newFaces;

    public OffsetFaceCommand(HalfEdgeMesh mesh, MeshFace face, double distance)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(face);
        if (!double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Смещение должно быть конечным.");
        }

        if (Math.Abs(distance) < MinCommitDistance)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Смещение должно отличаться от нуля.");
        }

        _mesh = mesh;
        _face = face;
        _distance = distance;
    }

    public string Description =>
        string.Create(CultureInfo.InvariantCulture, $"Offset {_distance:0.###} mm");

    /// <summary>Смещение контура, мм (со знаком).</summary>
    public double Distance => _distance;

    /// <summary>Новые вершины контура (создаются при первом выполнении).</summary>
    public IReadOnlyList<MeshVertex> NewVertices => _newVertices ?? [];

    /// <summary>Новые грани: кольцо квадов и внутренняя грань.</summary>
    public IReadOnlyList<MeshFace> NewFaces => _newFaces ?? [];

    public void Execute()
    {
        if (_newVertices is null)
        {
            IReadOnlyList<MeshVertex> ring = _mesh.GetRing(_face);
            var positions = new List<Vector3d>(ring.Count);
            foreach (MeshVertex vertex in ring)
            {
                positions.Add(vertex.Position);
            }

            Vector3d normal = MeshNormals.ComputeFaceNormal(_face).Normalized();
            IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(positions, normal, _distance);

            _newVertices = new List<MeshVertex>(contour.Count);
            foreach (Vector3d position in contour)
            {
                _newVertices.Add(_mesh.AddVertex(position));
            }

            _newRings = BuildRings(ring, _newVertices);
        }
        else
        {
            foreach (MeshVertex vertex in _newVertices)
            {
                _mesh.RestoreVertex(vertex);
            }
        }

        // сначала освободить направленные рёбра исходной грани — иначе AddFace/RestoreFace займут их ключи
        _erased = MeshEditOps.EraseFaces(_mesh, [_face]);

        if (_newFaces is null)
        {
            _newFaces = new List<MeshFace>(_newRings!.Count);
            foreach (IReadOnlyList<MeshVertex> faceRing in _newRings)
            {
                MeshFace created = _mesh.AddFace(faceRing);
                created.MaterialIndex = _face.MaterialIndex;
                _newFaces.Add(created);
            }
        }
        else
        {
            foreach (MeshFace created in _newFaces)
            {
                _mesh.RestoreFace(created);
            }
        }
    }

    public void Undo()
    {
        MeshEditOps.EraseFaces(_mesh, _newFaces!);
        foreach (MeshVertex vertex in _newVertices!)
        {
            _mesh.RemoveVertex(vertex);
        }

        MeshEditOps.RestoreFaces(_erased);
    }

    private static List<IReadOnlyList<MeshVertex>> BuildRings(
        IReadOnlyList<MeshVertex> ring,
        IReadOnlyList<MeshVertex> contour)
    {
        var rings = new List<IReadOnlyList<MeshVertex>>(ring.Count + 1);
        for (int i = 0; i < ring.Count; i++)
        {
            int next = (i + 1) % ring.Count;
            rings.Add([ring[i], ring[next], contour[next], contour[i]]);
        }

        rings.Add([.. contour]);
        return rings;
    }
}
