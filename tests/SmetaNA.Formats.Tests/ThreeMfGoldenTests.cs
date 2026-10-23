using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ThreeMf;
using SmetaNA.Formats.Validation;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Golden-меш тесты 3MF (задачи 4.2–4.4, DoD §5.3–§5.4): round-trip эталонных моделей
/// с точностью до 0,01 мм, совместимость единиц, отчёт о потерях.
/// Внешняя проверка в слайсерах (PrusaSlicer/Cura) — задача 4.6, здесь не выполняется.
/// </summary>
public sealed class ThreeMfGoldenTests
{
    [Theory]
    [InlineData("Cube")]
    [InlineData("Cup")]
    [InlineData("Stand")]
    public void RoundTrip_GoldenMesh_PreservesGeometryAndPrintability(string meshName)
    {
        MeshData mesh = GetMesh(meshName);
        byte[] file = ThreeMfExporter.Export(mesh);

        MeshImportResult result = ThreeMfImporter.Import(file);

        MeshAssert.EqualCorners(mesh, result.Mesh, 0.01);
        double expectedVolume = GetVolume(meshName);
        double actualVolume = MeshAssert.SignedVolume(result.Mesh);
        Assert.True(
            Math.Abs(actualVolume - expectedVolume) <= expectedVolume * 0.01,
            $"объём {meshName}: {actualVolume} вместо {expectedVolume}");
        PrintabilityReport report = PrintabilityValidator.Validate(result.Mesh);
        Assert.True(report.IsPrintable, report.ToText());
    }

    [Fact]
    public void RoundTrip_Cube_Keeps100mmDimensionWithinTolerance()
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cube());

        MeshData imported = ThreeMfImporter.Import(file).Mesh;

        Assert.Equal(100, SizeOn(imported, v => v.X), 2);
        Assert.Equal(100, SizeOn(imported, v => v.Y), 2);
        Assert.Equal(100, SizeOn(imported, v => v.Z), 2);
    }

    [Theory]
    [InlineData(LengthUnit.Millimeter)]
    [InlineData(LengthUnit.Centimeter)]
    [InlineData(LengthUnit.Meter)]
    [InlineData(LengthUnit.Inch)]
    public void RoundTrip_AnyUnit_StaysWithinTolerance(LengthUnit unit)
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cube(), new MeshExportOptions(unit));

        MeshImportResult result = ThreeMfImporter.Import(file);

        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
        Assert.Equal(100, SizeOn(result.Mesh, v => v.Z), 2);
    }

    [Fact]
    public void RoundTrip_ReportsLostFeatures()
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cup());

        MeshImportResult result = ThreeMfImporter.Import(file);

        MeshImportReport report = result.Report;
        Assert.Equal(MeshFileFormat.ThreeMf, report.Format);
        Assert.Equal(LengthUnit.Millimeter, report.SourceUnit);
        Assert.Equal(1.0, report.MillimetersPerUnit);
        Assert.Equal(28, report.TriangleCount);
        Assert.Equal(0, report.DegenerateTriangleCount);
        Assert.True(report.WeldedVertexCount >= 0, report.ToText());
        Assert.Contains("метаданные (Title, Designer и др.)", report.LostFeatures);
        Assert.Contains("имена объектов (name)", report.LostFeatures);
        Assert.Contains("идентификаторы объектов (UUID)", report.LostFeatures);
        Assert.Contains("Формат: ThreeMf", report.ToText());
        Assert.Contains("мм в единице", report.ToText());
    }

    private static MeshData GetMesh(string name) => name switch
    {
        "Cube" => GoldenMeshes.Cube(),
        "Cup" => GoldenMeshes.Cup(),
        "Stand" => GoldenMeshes.Stand(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static double GetVolume(string name) => name switch
    {
        "Cube" => GoldenMeshes.CubeVolumeMm3,
        "Cup" => GoldenMeshes.CupVolumeMm3,
        "Stand" => GoldenMeshes.StandVolumeMm3,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static double SizeOn(MeshData mesh, Func<Vector3d, double> coordinate)
    {
        double min = mesh.Positions.Min(coordinate);
        double max = mesh.Positions.Max(coordinate);
        return max - min;
    }
}
