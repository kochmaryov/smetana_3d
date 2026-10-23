namespace SmetaNA.Core.Geometry;

/// <summary>Вид дефекта грани меша.</summary>
public enum MeshDefectKind
{
    /// <summary>Нулевая площадь (коллинеарные или совпадающие вершины).</summary>
    ZeroArea,

    /// <summary>Вершина встречается в кольце грани более одного раза.</summary>
    RepeatedVertex,
}

/// <summary>Дефект грани: индекс грани в <see cref="HalfEdgeMesh.Faces"/> и вид.</summary>
public sealed record MeshDefect(int FaceIndex, MeshDefectKind Kind);

/// <summary>
/// Результат базовой валидации меша: размеры, связность, дегенераты
/// и эйлерова характеристика V−E+F.
/// </summary>
public sealed class MeshValidationResult
{
    public required int VertexCount { get; init; }
    public required int HalfEdgeCount { get; init; }

    /// <summary>Число рёбер (неориентированных, с учётом twin-пар).</summary>
    public required int EdgeCount { get; init; }

    public required int FaceCount { get; init; }

    /// <summary>Число связных компонент (по вершинам и рёбрам); изолированные вершины — отдельные компоненты.</summary>
    public required int ComponentCount { get; init; }

    /// <summary>Вершины без полурёбер (не участвуют в гранях).</summary>
    public required int IsolatedVertexCount { get; init; }

    /// <summary>Эйлерова характеристика V − E + F всего меша.</summary>
    public required int EulerCharacteristic { get; init; }

    public required bool IsWatertight { get; init; }

    /// <summary>
    /// Согласованность топологии: для замкнутого меша χ каждой компоненты с гранями
    /// должна быть чётной и ≤ 2 (χ = 2 − 2g); для незамкнутого проверка неприменима — true.
    /// </summary>
    public required bool IsEulerValid { get; init; }

    public required IReadOnlyList<MeshDefect> Defects { get; init; }

    /// <summary>Единая связная компонента (0 или 1 компонент).</summary>
    public bool IsConnected => ComponentCount <= 1;

    /// <summary>Меш без дефектов и с согласованной топологией; связность сюда не входит (две отдельные твёрдые части — валидная геометрия).</summary>
    public bool IsValid => Defects.Count == 0 && IsEulerValid;
}

/// <summary>Базовая валидация меша: связность, дегенераты, эйлерова характеристика для замкнутых мешей.</summary>
public static class MeshValidator
{
    /// <summary>
    /// Проверяет меш. <paramref name="degenerateEpsilon"/> — порог на длину нормали Ньюэлла
    /// (пропорциональна удвоенной площади грани), ниже которого грань считается вырожденной.
    /// </summary>
    public static MeshValidationResult Validate(HalfEdgeMesh mesh, double degenerateEpsilon = 1e-12)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!double.IsFinite(degenerateEpsilon) || degenerateEpsilon < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degenerateEpsilon), "Порог вырожденности должен быть неотрицательным конечным числом.");
        }

        int vertexCount = mesh.Vertices.Count;
        var indexOf = new Dictionary<MeshVertex, int>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        var parents = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            parents[i] = i;
        }

        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            Union(parents, indexOf[edge.Origin], indexOf[edge.Next!.Origin]);
        }

        var componentOfVertex = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            componentOfVertex[i] = Find(parents, i);
        }

        var rootIds = new Dictionary<int, int>();
        for (int i = 0; i < vertexCount; i++)
        {
            int root = componentOfVertex[i];
            if (!rootIds.TryGetValue(root, out int id))
            {
                id = rootIds.Count;
                rootIds[root] = id;
            }
        }

        int componentCount = rootIds.Count;

        int isolatedVertexCount = 0;
        var verticesPerComponent = new int[componentCount];
        for (int i = 0; i < vertexCount; i++)
        {
            verticesPerComponent[rootIds[componentOfVertex[i]]]++;
            if (mesh.Vertices[i].HalfEdge is null)
            {
                isolatedVertexCount++;
            }
        }

        int edgeCount = 0;
        var edgesPerComponent = new int[componentCount];
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            bool isCanonical = edge.Twin is null || indexOf[edge.Origin] < indexOf[edge.Twin.Origin];
            if (isCanonical)
            {
                edgeCount++;
                edgesPerComponent[rootIds[componentOfVertex[indexOf[edge.Origin]]]]++;
            }
        }

        var facesPerComponent = new int[componentCount];
        var defects = new List<MeshDefect>();
        for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
        {
            MeshFace face = mesh.Faces[faceIndex];
            var ring = new List<MeshVertex>();
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                ring.Add(edge.Origin);
            }

            facesPerComponent[rootIds[componentOfVertex[indexOf[ring[0]]]]]++;

            bool repeated = false;
            for (int i = 0; i < ring.Count && !repeated; i++)
            {
                for (int j = i + 1; j < ring.Count; j++)
                {
                    if (ReferenceEquals(ring[i], ring[j]))
                    {
                        repeated = true;
                        break;
                    }
                }
            }

            if (repeated)
            {
                defects.Add(new MeshDefect(faceIndex, MeshDefectKind.RepeatedVertex));
            }

            if (MeshNormals.ComputeFaceNormal(face).Length() <= degenerateEpsilon)
            {
                defects.Add(new MeshDefect(faceIndex, MeshDefectKind.ZeroArea));
            }
        }

        bool isWatertight = mesh.IsWatertight;
        bool isEulerValid = true;
        if (isWatertight)
        {
            for (int component = 0; component < componentCount; component++)
            {
                if (facesPerComponent[component] == 0)
                {
                    continue;
                }

                int chi = verticesPerComponent[component] - edgesPerComponent[component] + facesPerComponent[component];
                if (chi > 2 || chi % 2 != 0)
                {
                    isEulerValid = false;
                    break;
                }
            }
        }

        return new MeshValidationResult
        {
            VertexCount = vertexCount,
            HalfEdgeCount = mesh.HalfEdges.Count,
            EdgeCount = edgeCount,
            FaceCount = mesh.Faces.Count,
            ComponentCount = componentCount,
            IsolatedVertexCount = isolatedVertexCount,
            EulerCharacteristic = vertexCount - edgeCount + mesh.Faces.Count,
            IsWatertight = isWatertight,
            IsEulerValid = isEulerValid,
            Defects = defects,
        };
    }

    private static int Find(int[] parents, int vertex)
    {
        while (parents[vertex] != vertex)
        {
            parents[vertex] = parents[parents[vertex]];
            vertex = parents[vertex];
        }

        return vertex;
    }

    private static void Union(int[] parents, int a, int b)
    {
        int rootA = Find(parents, a);
        int rootB = Find(parents, b);
        if (rootA != rootB)
        {
            parents[rootB] = rootA;
        }
    }
}
