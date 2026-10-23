using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats;

/// <summary>
/// Сборка меша из треугольного супа: сварка одинаковых позиций (ключ — точная
/// тройка double после приведения к мм) и регистрация вырожденных треугольников.
/// Общий путь нормализации для импортёров STL и OBJ.
/// </summary>
internal sealed class TriangleSoupAccumulator
{
    private readonly bool _weldVertices;
    private readonly List<Vector3d> _positions = [];
    private readonly List<int> _indices = [];
    private readonly List<int> _degenerateTriangles = [];
    private readonly Dictionary<(double X, double Y, double Z), int> _weldMap = [];

    public TriangleSoupAccumulator(bool weldVertices)
    {
        _weldVertices = weldVertices;
    }

    /// <summary>Записей вершин, встреченных в файле (до сварки).</summary>
    public int SourceVertexCount { get; private set; }

    public IReadOnlyList<int> DegenerateTriangleIndices => _degenerateTriangles;

    public void AddTriangle(Vector3d a, Vector3d b, Vector3d c)
    {
        SourceVertexCount += 3;
        int ia = GetOrAdd(a);
        int ib = GetOrAdd(b);
        int ic = GetOrAdd(c);

        int triangleIndex = _indices.Count / 3;
        if (ia == ib || ib == ic || ia == ic || TriangleGeometry.IsDegenerate(a, b, c))
        {
            _degenerateTriangles.Add(triangleIndex);
        }

        _indices.Add(ia);
        _indices.Add(ib);
        _indices.Add(ic);
    }

    public MeshData ToMeshData() => new([.. _positions], [.. _indices]);

    private int GetOrAdd(Vector3d position)
    {
        if (!_weldVertices)
        {
            _positions.Add(position);
            return _positions.Count - 1;
        }

        var key = (position.X, position.Y, position.Z);
        if (_weldMap.TryGetValue(key, out int index))
        {
            return index;
        }

        _positions.Add(position);
        index = _positions.Count - 1;
        _weldMap[key] = index;
        return index;
    }
}
