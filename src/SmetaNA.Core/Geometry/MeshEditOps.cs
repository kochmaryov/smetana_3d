namespace SmetaNA.Core.Geometry;

/// <summary>
/// Перенос вершины: снимок позиции до/после — атомарное изменение меша,
/// из которого строятся команды истории (<c>Undo</c> возвращает <see cref="From"/>).
/// </summary>
/// <param name="Vertex">Перемещаемая вершина.</param>
/// <param name="From">Позиция до переноса.</param>
/// <param name="To">Позиция после переноса.</param>
public readonly record struct VertexMove(MeshVertex Vertex, Vector3d From, Vector3d To)
{
    public Vector3d Delta => To - From;
}

/// <summary>
/// Правки меша: перенос вершин/граней со склеиванием (stickiness, спека M2).
/// <para>
/// Склеивание — ключевое поведение SketchUp: при деформации грани прилегающие грани
/// и рёбра остаются присоединёнными к деформируемой геометрии, угол остаётся замкнутым.
/// Механизм: (1) топологическое склеивание — соседние грани делят вершины с кольцом
/// деформируемой грани, поэтому перенос вершин автоматически тянет соседей;
/// (2) позиционное склеивание — вершины, совпадающие с переносимыми по позиции
/// (вплоть до допуска), переносятся вместе с ними, даже если принадлежат другим
/// мешам одного контекста (несваренная геометрия, соседние объекты).
/// </para>
/// </summary>
public static class MeshEditOps
{
    /// <summary>Допуск совпадения позиций при склеивании, мм.</summary>
    public const double DefaultToleranceMm = 1e-6;

    /// <summary>
    /// План переноса вершин: семена (<paramref name="seeds"/>) переносятся всегда,
    /// к ним добавляются «склеенные» вершины из <paramref name="glueScope"/>,
    /// позиции которых совпадают с позициями семян (см. <paramref name="tolerance"/>).
    /// </summary>
    public static IReadOnlyList<VertexMove> PlanMove(
        IReadOnlyList<MeshVertex> seeds,
        IEnumerable<MeshVertex> glueScope,
        Vector3d delta,
        double tolerance = DefaultToleranceMm)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(glueScope);
        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Допуск не может быть отрицательным.");
        }

        var moves = new List<VertexMove>();
        var included = new HashSet<MeshVertex>();
        var seedPositions = new List<Vector3d>();

        foreach (MeshVertex seed in seeds)
        {
            if (seed is null)
            {
                throw new ArgumentException("Семена не содержат null.", nameof(seeds));
            }

            if (!included.Add(seed))
            {
                continue;
            }

            seedPositions.Add(seed.Position);
            moves.Add(new VertexMove(seed, seed.Position, seed.Position + delta));
        }

        double toleranceSquared = tolerance * tolerance;
        foreach (MeshVertex vertex in glueScope)
        {
            if (vertex is null || included.Contains(vertex))
            {
                continue;
            }

            if (!IsNearAny(vertex.Position, seedPositions, toleranceSquared))
            {
                continue;
            }

            included.Add(vertex);
            moves.Add(new VertexMove(vertex, vertex.Position, vertex.Position + delta));
        }

        return moves;
    }

    /// <summary>План переноса вершин без позиционного склеивания.</summary>
    public static IReadOnlyList<VertexMove> PlanMove(IReadOnlyList<MeshVertex> seeds, Vector3d delta)
        => PlanMove(seeds, Array.Empty<MeshVertex>(), delta, 0);

    /// <summary>
    /// План переноса грани вдоль её нормали на расстояние (push/pull) со склеиванием:
    /// кольцо грани — семена, склеенные вершины берутся из <paramref name="glueScope"/>
    /// (по умолчанию — весь меш).
    /// </summary>
    public static IReadOnlyList<VertexMove> PlanFaceMoveAlongNormal(
        HalfEdgeMesh mesh,
        MeshFace face,
        double distance,
        IEnumerable<MeshVertex>? glueScope = null,
        double tolerance = DefaultToleranceMm)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(face);
        if (!double.IsFinite(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Расстояние должно быть конечным.");
        }

        Vector3d normal = MeshNormals.ComputeFaceNormal(face).Normalized();
        if (normal.LengthSquared() <= 0)
        {
            throw new InvalidOperationException("Нормаль грани не определена (вырожденная грань).");
        }

        IReadOnlyList<MeshVertex> seeds = mesh.GetRing(face);
        return PlanMove(seeds, glueScope ?? mesh.Vertices, normal * distance, tolerance);
    }

    /// <summary>Применить план (позиции → <see cref="VertexMove.To"/>).</summary>
    public static void Apply(IReadOnlyList<VertexMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        foreach (VertexMove move in moves)
        {
            move.Vertex.Position = move.To;
        }
    }

    /// <summary>Откатить план (позиции → <see cref="VertexMove.From"/>).</summary>
    public static void Revert(IReadOnlyList<VertexMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        foreach (VertexMove move in moves)
        {
            move.Vertex.Position = move.From;
        }
    }

    /// <summary>
    /// Грани, зависящие от переносимых вершин (каскадное обновление соседних граней):
    /// каждая грань, кольцо которой содержит переносимую вершину, деформируется
    /// вместе с ней.
    /// </summary>
    public static IReadOnlyList<MeshFace> CollectDependentFaces(
        HalfEdgeMesh mesh,
        IEnumerable<MeshVertex> vertices)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);

        var set = new HashSet<MeshVertex>(vertices);
        var faces = new List<MeshFace>();
        foreach (MeshFace face in mesh.Faces)
        {
            foreach (MeshVertex vertex in mesh.GetRing(face))
            {
                if (set.Contains(vertex))
                {
                    faces.Add(face);
                    break;
                }
            }
        }

        return faces;
    }

    /// <summary>Центроид грани (среднее кольца вершин).</summary>
    public static Vector3d FaceCentroid(MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        Vector3d sum = Vector3d.Zero;
        int count = 0;
        MeshHalfEdge edge = face.HalfEdge;
        do
        {
            sum += edge.Origin.Position;
            count++;
            edge = edge.Next!;
        }
        while (edge != face.HalfEdge);
        return count == 0 ? Vector3d.Zero : sum * (1.0 / count);
    }

    private static bool IsNearAny(Vector3d position, List<Vector3d> candidates, double toleranceSquared)
    {
        foreach (Vector3d candidate in candidates)
        {
            if ((position - candidate).LengthSquared() <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }
}
