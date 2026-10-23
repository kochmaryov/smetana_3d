using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.Validation;

/// <summary>
/// Валидатор печатаемости (задача 4.4, модуль M8): замкнутость
/// (<see cref="HalfEdgeMesh.IsWatertight"/>), ориентация нормалей, дегенераты.
/// Самопересечения в этой версии НЕ анализируются — проверка помечена в отчёте
/// как задел (NotImplemented) и на готовность не влияет.
/// </summary>
public static class PrintabilityValidator
{
    private const double VolumeEpsilonMm3 = 1e-9;
    private const string SelfIntersectionNote =
        "Самопересечения: анализ не реализован (задел M8 v1.x), проверка помечена как NotImplemented.";

    /// <summary>Проверка печатаемости меша в миллиметрах.</summary>
    public static PrintabilityReport Validate(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ValidateIndices(mesh);

        HalfEdgeMesh? topology = null;
        string? topologyError = null;
        try
        {
            topology = HalfEdgeMesh.FromTriangles(mesh.Positions, mesh.Indices);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            topologyError = ex.Message;
        }

        return BuildReport(mesh, topology?.IsWatertight ?? false, topologyError);
    }

    /// <summary>Проверка печатаемости топологии меша (watertight — по контракту HalfEdgeMesh).</summary>
    public static PrintabilityReport Validate(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return BuildReport(mesh.ToTriangles(), mesh.IsWatertight, null);
    }

    private static PrintabilityReport BuildReport(MeshData data, bool watertight, string? topologyError)
    {
        int triangleCount = data.Indices.Count / 3;
        if (triangleCount == 0)
        {
            return new PrintabilityReport(
                IsPrintable: false,
                Watertight: CheckStatus.Failed,
                NormalOrientation: CheckStatus.NotApplicable,
                DegenerateGeometry: CheckStatus.NotApplicable,
                SelfIntersection: CheckStatus.NotImplemented,
                TriangleCount: 0,
                VertexCount: data.Positions.Count,
                BoundaryEdgeCount: 0,
                NonManifoldEdgeCount: 0,
                DuplicateDirectedEdgeCount: 0,
                DegenerateTriangleCount: 0,
                IsOutwardOriented: null,
                SignedVolumeMm3: 0,
                Issues:
                [
                    new PrintabilityIssue(PrintabilityIssueCode.EmptyMesh, IssueSeverity.Error, 1, "Меш не содержит треугольников."),
                ],
                Notes: [SelfIntersectionNote]);
        }

        var degenerateTriangles = new List<int>();
        var directedEdges = new Dictionary<(int From, int To), int>();
        var undirectedEdges = new Dictionary<(int Min, int Max), List<int>>();
        double volume = 0;

        for (int t = 0; t < triangleCount; t++)
        {
            int i0 = data.Indices[(3 * t) + 0];
            int i1 = data.Indices[(3 * t) + 1];
            int i2 = data.Indices[(3 * t) + 2];
            Vector3d a = data.Positions[i0];
            Vector3d b = data.Positions[i1];
            Vector3d c = data.Positions[i2];

            volume += TriangleGeometry.SignedVolumeContribution(a, b, c);
            if (i0 == i1 || i1 == i2 || i0 == i2 || TriangleGeometry.IsDegenerate(a, b, c))
            {
                degenerateTriangles.Add(t);
            }

            CountEdge(directedEdges, undirectedEdges, i0, i1);
            CountEdge(directedEdges, undirectedEdges, i1, i2);
            CountEdge(directedEdges, undirectedEdges, i2, i0);
        }

        int boundaryEdges = 0;
        int nonManifoldEdges = 0;
        int inconsistentSharedEdges = 0;
        foreach (List<int> directions in undirectedEdges.Values)
        {
            if (directions.Count == 1)
            {
                boundaryEdges++;
            }
            else if (directions.Count > 2)
            {
                nonManifoldEdges++;
            }
            else if (directions[0] == directions[1])
            {
                inconsistentSharedEdges++;
            }
        }

        int duplicateDirectedEdges = 0;
        foreach (int count in directedEdges.Values)
        {
            if (count > 1)
            {
                duplicateDirectedEdges += count - 1;
            }
        }

        bool orientationConsistent = duplicateDirectedEdges == 0 && inconsistentSharedEdges == 0;
        bool? outwardOriented = null;
        CheckStatus orientationStatus;
        var issues = new List<PrintabilityIssue>();
        var notes = new List<string> { SelfIntersectionNote };

        if (topologyError is not null)
        {
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.TopologyBuildFailed,
                IssueSeverity.Error,
                1,
                $"Топология HalfEdgeMesh не строится: {topologyError}"));
        }

        if (boundaryEdges > 0)
        {
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.BoundaryEdges,
                IssueSeverity.Error,
                boundaryEdges,
                "Незамкнутый контур: есть рёбра без парной грани (watertight не выполнен)."));
        }

        if (nonManifoldEdges > 0)
        {
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.NonManifoldEdges,
                IssueSeverity.Error,
                nonManifoldEdges,
                "Не 2-манифолден: ребро принадлежит более чем двум граням."));
        }

        if (duplicateDirectedEdges > 0)
        {
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.InconsistentOrientation,
                IssueSeverity.Error,
                duplicateDirectedEdges,
                "Рассогласована ориентация нормалей: общее ребро обходится в одну сторону."));
        }

        if (!orientationConsistent)
        {
            orientationStatus = CheckStatus.Failed;
        }
        else if (!watertight)
        {
            orientationStatus = CheckStatus.NotApplicable;
        }
        else if (volume > VolumeEpsilonMm3)
        {
            orientationStatus = CheckStatus.Passed;
            outwardOriented = true;
        }
        else if (volume < -VolumeEpsilonMm3)
        {
            orientationStatus = CheckStatus.Failed;
            outwardOriented = false;
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.InvertedOrientation,
                IssueSeverity.Warning,
                triangleCount,
                "Нормали направлены внутрь: меш замкнут, но инвертирован (инвертируйте нормали; слайсеры часто чинят это автоматически)."));
            notes.Add("Модель замкнута, но ориентирована внутрь — нормали требуют инверсии.");
        }
        else
        {
            orientationStatus = CheckStatus.Failed;
            notes.Add("Ориентированный объём ≈ 0 — направление «наружу» не определено.");
        }

        CheckStatus degenerateStatus = degenerateTriangles.Count == 0
            ? CheckStatus.Passed
            : CheckStatus.Failed;
        if (degenerateTriangles.Count > 0)
        {
            issues.Add(new PrintabilityIssue(
                PrintabilityIssueCode.DegenerateTriangles,
                IssueSeverity.Warning,
                degenerateTriangles.Count,
                "Вырожденные треугольники (нулевая площадь или повтор вершин) — зачистите перед экспортом."));
        }

        bool isPrintable = watertight
            && orientationStatus == CheckStatus.Passed
            && degenerateStatus == CheckStatus.Passed;

        return new PrintabilityReport(
            IsPrintable: isPrintable,
            Watertight: watertight ? CheckStatus.Passed : CheckStatus.Failed,
            NormalOrientation: orientationStatus,
            DegenerateGeometry: degenerateStatus,
            SelfIntersection: CheckStatus.NotImplemented,
            TriangleCount: triangleCount,
            VertexCount: data.Positions.Count,
            BoundaryEdgeCount: boundaryEdges,
            NonManifoldEdgeCount: nonManifoldEdges,
            DuplicateDirectedEdgeCount: duplicateDirectedEdges,
            DegenerateTriangleCount: degenerateTriangles.Count,
            IsOutwardOriented: outwardOriented,
            SignedVolumeMm3: volume,
            Issues: issues,
            Notes: notes);
    }

    private static void CountEdge(
        Dictionary<(int From, int To), int> directedEdges,
        Dictionary<(int Min, int Max), List<int>> undirectedEdges,
        int from,
        int to)
    {
        var directedKey = (from, to);
        directedEdges[directedKey] = directedEdges.TryGetValue(directedKey, out int count) ? count + 1 : 1;

        var undirectedKey = (Math.Min(from, to), Math.Max(from, to));
        if (!undirectedEdges.TryGetValue(undirectedKey, out List<int>? directions))
        {
            directions = [];
            undirectedEdges[undirectedKey] = directions;
        }

        directions.Add(from < to ? 1 : -1);
    }

    private static void ValidateIndices(MeshData mesh)
    {
        for (int i = 0; i < mesh.Indices.Count; i++)
        {
            int index = mesh.Indices[i];
            if (index < 0 || index >= mesh.Positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(mesh), $"Индекс вершины {index} вне диапазона 0..{mesh.Positions.Count - 1}.");
            }
        }
    }
}
