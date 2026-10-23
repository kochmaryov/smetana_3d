namespace SmetaNA.Core.Geometry;

/// <summary>
/// Ограничивающий параллелепипед (AABB) двойной точности.
/// <see cref="Empty"/> — нейтральный элемент для <see cref="Union"/>: Empty.Union(b) = b.
/// Внимание: <c>default(BoundingBox3d)</c> — ящик-точка в начале координат; для накопления используйте <see cref="Empty"/>.
/// </summary>
public readonly record struct BoundingBox3d(Vector3d Min, Vector3d Max)
{
    /// <summary>Пустой ящик (Min &gt; Max по всем осям).</summary>
    public static readonly BoundingBox3d Empty = new(
        new Vector3d(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Vector3d(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public Vector3d Center => (Min + Max) * 0.5;

    public Vector3d Size => Max - Min;

    /// <summary>Длина диагонали; для пустого ящика — 0.</summary>
    public double DiagonalLength => IsEmpty ? 0 : Size.Length();

    /// <summary>Ящик, содержащий все точки; пустая последовательность даёт <see cref="Empty"/>.</summary>
    public static BoundingBox3d FromPoints(IEnumerable<Vector3d> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var box = Empty;
        foreach (Vector3d point in points)
        {
            box = box.Union(point);
        }

        return box;
    }

    /// <summary>Ящик по вершинам меша (без трансформации).</summary>
    public static BoundingBox3d FromMesh(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var box = Empty;
        foreach (MeshVertex vertex in mesh.Vertices)
        {
            box = box.Union(vertex.Position);
        }

        return box;
    }

    /// <summary>Ящик по позициям треугольного супа.</summary>
    public static BoundingBox3d FromMeshData(MeshData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var box = Empty;
        foreach (Vector3d position in data.Positions)
        {
            box = box.Union(position);
        }

        return box;
    }

    public BoundingBox3d Union(BoundingBox3d other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new BoundingBox3d(
            new Vector3d(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
            new Vector3d(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));
    }

    public BoundingBox3d Union(Vector3d point) => Union(new BoundingBox3d(point, point));

    /// <summary>Расширение на margin в каждую сторону; отрицательное значение сжимает.</summary>
    public BoundingBox3d Expanded(double margin)
    {
        if (IsEmpty)
        {
            return this;
        }

        var delta = new Vector3d(margin, margin, margin);
        return new BoundingBox3d(Min - delta, Max + delta);
    }

    public bool Contains(Vector3d point) => !IsEmpty
        && point.X >= Min.X && point.X <= Max.X
        && point.Y >= Min.Y && point.Y <= Max.Y
        && point.Z >= Min.Z && point.Z <= Max.Z;

    /// <summary>
    /// AABB преобразованного ящика: трансформируются все восемь углов,
    /// результат снова приводится к осям координат.
    /// </summary>
    public BoundingBox3d Transformed(Transform3d transform)
    {
        if (IsEmpty)
        {
            return Empty;
        }

        var result = Empty;
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vector3d(
                (corner & 1) == 0 ? Min.X : Max.X,
                (corner & 2) == 0 ? Min.Y : Max.Y,
                (corner & 4) == 0 ? Min.Z : Max.Z);
            result = result.Union(transform.TransformPoint(point));
        }

        return result;
    }
}
