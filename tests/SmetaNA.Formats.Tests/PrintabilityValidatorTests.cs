using SmetaNA.Core.Geometry;
using SmetaNA.Formats.Validation;
using Xunit;

namespace SmetaNA.Formats.Tests;

public sealed class PrintabilityValidatorTests
{
    [Theory]
    [InlineData("Cube")]
    [InlineData("Cup")]
    [InlineData("Stand")]
    public void Validate_GoldenMeshes_ArePrintable(string meshName)
    {
        MeshData mesh = GetMesh(meshName);

        PrintabilityReport report = PrintabilityValidator.Validate(mesh);

        Assert.True(report.IsPrintable, report.ToText());
        Assert.Equal(CheckStatus.Passed, report.Watertight);
        Assert.Equal(CheckStatus.Passed, report.NormalOrientation);
        Assert.Equal(CheckStatus.Passed, report.DegenerateGeometry);
        Assert.Equal(CheckStatus.NotImplemented, report.SelfIntersection);
        Assert.True(report.IsOutwardOriented);
        Assert.Equal(0, report.BoundaryEdgeCount);
        Assert.Equal(0, report.NonManifoldEdgeCount);
        Assert.Empty(report.Issues);
        Assert.Contains(report.Notes, n => n.Contains("задел", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Cube", 1_000_000)]
    [InlineData("Cup", 100_500)]
    [InlineData("Stand", 228_000)]
    public void Validate_GoldenMeshes_SignedVolumeMatchesExpected(string meshName, double expectedVolumeMm3)
    {
        PrintabilityReport report = PrintabilityValidator.Validate(GetMesh(meshName));

        Assert.Equal(expectedVolumeMm3, report.SignedVolumeMm3, 3);
    }

    [Fact]
    public void Validate_HalfEdgeMesh_UsesIsWatertight()
    {
        MeshData cube = GoldenMeshes.Cube();
        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles([.. cube.Positions], [.. cube.Indices]);

        PrintabilityReport report = PrintabilityValidator.Validate(mesh);

        Assert.True(report.IsPrintable, report.ToText());
        Assert.Equal(CheckStatus.Passed, report.Watertight);
    }

    [Fact]
    public void Validate_OpenMesh_DetectsBoundaryEdges()
    {
        MeshData cube = GoldenMeshes.Cube();
        var open = new MeshData(cube.Positions, [.. cube.Indices.Take(30)]);

        PrintabilityReport report = PrintabilityValidator.Validate(open);

        Assert.False(report.IsPrintable);
        Assert.Equal(CheckStatus.Failed, report.Watertight);
        Assert.Equal(4, report.BoundaryEdgeCount);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.BoundaryEdges && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Validate_FullyInvertedMesh_DetectsInvertedOrientation()
    {
        MeshData cube = GoldenMeshes.Cube();
        var inverted = new MeshData(cube.Positions, ReverseTriangles(cube.Indices));

        PrintabilityReport report = PrintabilityValidator.Validate(inverted);

        Assert.False(report.IsPrintable);
        Assert.Equal(CheckStatus.Passed, report.Watertight);
        Assert.Equal(CheckStatus.Failed, report.NormalOrientation);
        Assert.False(report.IsOutwardOriented);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.InvertedOrientation);
    }

    [Fact]
    public void Validate_OneFlippedTriangle_DetectsInconsistentOrientation()
    {
        MeshData cube = GoldenMeshes.Cube();
        int[] indices = [.. cube.Indices];
        (indices[0], indices[1]) = (indices[1], indices[0]);
        var broken = new MeshData(cube.Positions, indices);

        PrintabilityReport report = PrintabilityValidator.Validate(broken);

        Assert.False(report.IsPrintable);
        Assert.Equal(CheckStatus.Failed, report.NormalOrientation);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.InconsistentOrientation);
    }

    [Fact]
    public void Validate_DegenerateTriangle_IsReported()
    {
        MeshData cube = GoldenMeshes.Cube();
        var withDegenerate = new MeshData(
            [.. cube.Positions, new Vector3d(50, 50, 0)],
            [.. cube.Indices, 0, 8, 8]);

        PrintabilityReport report = PrintabilityValidator.Validate(withDegenerate);

        Assert.False(report.IsPrintable);
        Assert.Equal(CheckStatus.Failed, report.DegenerateGeometry);
        Assert.Equal(1, report.DegenerateTriangleCount);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.DegenerateTriangles && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void Validate_NonManifoldEdge_IsReported()
    {
        var mesh = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, -1, 0), new Vector3d(0, 0, 1)],
            [0, 1, 2, 1, 0, 3, 0, 1, 4]);

        PrintabilityReport report = PrintabilityValidator.Validate(mesh);

        Assert.False(report.IsPrintable);
        Assert.Equal(1, report.NonManifoldEdgeCount);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.NonManifoldEdges);
    }

    [Fact]
    public void Validate_EmptyMesh_ReportsEmptyMesh()
    {
        PrintabilityReport report = PrintabilityValidator.Validate(new MeshData([], []));

        Assert.False(report.IsPrintable);
        Assert.Equal(CheckStatus.Failed, report.Watertight);
        Assert.Equal(CheckStatus.NotApplicable, report.NormalOrientation);
        Assert.Equal(CheckStatus.NotImplemented, report.SelfIntersection);
        Assert.Contains(report.Issues, i => i.Code == PrintabilityIssueCode.EmptyMesh);
    }

    [Fact]
    public void Validate_IndexOutOfRange_Throws()
    {
        var mesh = new MeshData([new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)], [0, 1, 5]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PrintabilityValidator.Validate(mesh));
    }

    [Fact]
    public void Validate_ToText_ContainsSummary()
    {
        string text = PrintabilityValidator.Validate(GoldenMeshes.Cube()).ToText();

        Assert.Contains("Печатаемость: ГОТОВА К ПЕЧАТИ", text);
        Assert.Contains("Замкнутость (watertight): пройдена", text);
        Assert.Contains("Самопересечения: не реализована (задел)", text);
    }

    private static MeshData GetMesh(string name) => name switch
    {
        "Cube" => GoldenMeshes.Cube(),
        "Cup" => GoldenMeshes.Cup(),
        "Stand" => GoldenMeshes.Stand(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static int[] ReverseTriangles(IReadOnlyList<int> indices)
    {
        int[] result = [.. indices];
        for (int i = 0; i < result.Length; i += 3)
        {
            (result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
        }

        return result;
    }
}
