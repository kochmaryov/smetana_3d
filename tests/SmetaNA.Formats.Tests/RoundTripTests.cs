using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.Validation;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>Round-trip экспорт→импорт на эталонных моделях: точность до 0,01 мм (DoD §5.4).</summary>
public sealed class RoundTripTests
{
    public static TheoryData<string, MeshFileFormat> GoldenMeshesAndFormats()
    {
        var data = new TheoryData<string, MeshFileFormat>();
        foreach (string name in new[] { "Cube", "Cup", "Stand" })
        {
            foreach (MeshFileFormat format in Enum.GetValues<MeshFileFormat>())
            {
                data.Add(name, format);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenMeshesAndFormats))]
    public void RoundTrip_ExportImport_PreservesGeometryWithinTolerance(string meshName, MeshFileFormat format)
    {
        MeshData mesh = GetMesh(meshName);
        byte[] file = MeshFile.Export(mesh, format);

        MeshImportResult result = MeshFile.Import(file);

        MeshAssert.EqualCorners(mesh, result.Mesh, 0.01);
        double expectedVolume = GetVolume(meshName);
        double actualVolume = MeshAssert.SignedVolume(result.Mesh);
        Assert.True(
            Math.Abs(actualVolume - expectedVolume) <= expectedVolume * 0.01,
            $"объём {meshName}/{format}: {actualVolume} вместо {expectedVolume}");
        PrintabilityReport report = PrintabilityValidator.Validate(result.Mesh);
        Assert.True(report.IsPrintable, report.ToText());
    }

    [Theory]
    [MemberData(nameof(GoldenMeshesAndFormats))]
    public void RoundTrip_ReExport_IsStable(string meshName, MeshFileFormat format)
    {
        MeshData mesh = GetMesh(meshName);
        byte[] first = MeshFile.Export(mesh, format);
        byte[] normalized = MeshFile.Export(MeshFile.Import(first).Mesh, format);
        byte[] again = MeshFile.Export(MeshFile.Import(normalized).Mesh, format);

        Assert.Equal(normalized, again);
        if (format != MeshFileFormat.Obj)
        {
            // STL — треугольный суп: нормализация не меняет порядок треугольников и углов.
            Assert.Equal(first, normalized);
        }
    }

    [Theory]
    [InlineData(MeshFileFormat.BinaryStl)]
    [InlineData(MeshFileFormat.AsciiStl)]
    [InlineData(MeshFileFormat.Obj)]
    public void RoundTrip_Cube_Keeps100mmDimensionWithinTolerance(MeshFileFormat format)
    {
        byte[] file = MeshFile.Export(GoldenMeshes.Cube(), format);

        MeshData imported = MeshFile.Import(file).Mesh;

        Assert.Equal(100, SizeOn(imported, v => v.X), 2);
        Assert.Equal(100, SizeOn(imported, v => v.Y), 2);
        Assert.Equal(100, SizeOn(imported, v => v.Z), 2);
    }

    [Theory]
    [InlineData(MeshFileFormat.BinaryStl)]
    [InlineData(MeshFileFormat.AsciiStl)]
    [InlineData(MeshFileFormat.Obj)]
    public void RoundTrip_InchUnits_StayWithinTolerance(MeshFileFormat format)
    {
        byte[] file = MeshFile.Export(GoldenMeshes.Cube(), format, new MeshExportOptions(LengthUnit.Inch));

        MeshImportResult result = MeshFile.Import(file, new MeshImportOptions(LengthUnit.Inch));

        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
        Assert.Equal(100, SizeOn(result.Mesh, v => v.X), 2);
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
