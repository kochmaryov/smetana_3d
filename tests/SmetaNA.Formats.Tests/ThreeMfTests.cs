using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Lib3MF;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ThreeMf;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Формат 3MF (задача 4.2): экспорт/импорт через lib3mf, контроль единиц (база — мм),
/// метаданные, отчёт о потерях, байтовая стабильность.
/// </summary>
public sealed class ThreeMfTests
{
    [Fact]
    public void Export_MatchesGoldenFixture_Cube()
    {
        // Сравнение с golden-фикстурой — по записям ZIP с нормализацией переводов строк:
        // нативный lib3mf пишет XML с платформенными окончаниями (CRLF на Windows, LF на Linux),
        // поэтому байты контейнера межплатформенно различаются при идентичном содержимом.
        AssertZipMatchesGolden(Fixtures.Read("cube.3mf"), ThreeMfExporter.Export(GoldenMeshes.Cube()));
    }

    [Fact]
    public void Export_IsByteStable()
    {
        Assert.Equal(ThreeMfExporter.Export(GoldenMeshes.Stand()), ThreeMfExporter.Export(GoldenMeshes.Stand()));
    }

    [Theory]
    [InlineData("Cube")]
    [InlineData("Cup")]
    [InlineData("Stand")]
    public void ExportImport_RoundTrip_IsByteStable(string meshName)
    {
        MeshData mesh = GetMesh(meshName);
        byte[] first = ThreeMfExporter.Export(mesh);

        byte[] normalized = ThreeMfExporter.Export(ThreeMfImporter.Import(first).Mesh);

        Assert.Equal(first, normalized);
    }

    [Fact]
    public void Export_DeclaresUnitsAndMetadata()
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cube());

        string modelXml = ReadModelXml(file);
        Assert.Contains("unit=\"millimeter\"", modelXml);
        Assert.Contains("<metadata name=\"Title\">SmetaNA</metadata>", modelXml);
        Assert.Contains("<metadata name=\"Application\">SmetaNA</metadata>", modelXml);
    }

    [Fact]
    public void Export_WritesCustomMetadata()
    {
        var options = new MeshExportOptions(
            Metadata: new Dictionary<string, string>
            {
                ["Title"] = "Корпус",
                ["Designer"] = "Школа №1",
                ["License"] = "CC-BY",
            });

        string modelXml = ReadModelXml(ThreeMfExporter.Export(GoldenMeshes.Cube(), options));

        Assert.Contains("<metadata name=\"Title\">Корпус</metadata>", modelXml);
        Assert.Contains("<metadata name=\"Designer\">Школа №1</metadata>", modelXml);
        Assert.Contains("urn:smetana:metadata", modelXml);
        Assert.Contains("License", modelXml);
        Assert.Contains("preserve=\"1\"", modelXml);
        Assert.Contains("CC-BY", modelXml);
    }

    [Theory]
    [InlineData(LengthUnit.Millimeter, "millimeter", 1.0)]
    [InlineData(LengthUnit.Centimeter, "centimeter", 0.1)]
    [InlineData(LengthUnit.Meter, "meter", 0.001)]
    [InlineData(LengthUnit.Inch, "inch", 1.0 / 25.4)]
    public void Export_Unit_ScalesAndDeclaresUnits(LengthUnit unit, string declaredUnit, double coordinateFactor)
    {
        string modelXml = ReadModelXml(ThreeMfExporter.Export(GoldenMeshes.Cube(), new MeshExportOptions(unit)));

        Assert.Contains($"unit=\"{declaredUnit}\"", modelXml);
        MatchCollection vertices = Regex.Matches(modelXml, "<vertex x=\"([^\"]+)\" y=\"([^\"]+)\" z=\"([^\"]+)\"");
        Assert.True(vertices.Count > 0, "в модели нет вершин");
        double maxX = vertices
            .Cast<Match>()
            .Max(v => double.Parse(v.Groups[1].Value, CultureInfo.InvariantCulture));
        Assert.Equal(100 * coordinateFactor, maxX, 6);
    }

    [Theory]
    [InlineData(LengthUnit.Millimeter)]
    [InlineData(LengthUnit.Centimeter)]
    [InlineData(LengthUnit.Meter)]
    [InlineData(LengthUnit.Inch)]
    public void Import_NormalizesFileUnitsToMillimeters(LengthUnit unit)
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cube(), new MeshExportOptions(unit));

        MeshImportResult result = ThreeMfImporter.Import(file);

        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
        Assert.Equal(100, SizeOn(result.Mesh, v => v.X), 2);
        Assert.Equal(unit, result.Report.SourceUnit);
        Assert.Equal(unit.ToMillimeters(), result.Report.MillimetersPerUnit, 6);
        Assert.Null(result.Report.SourceUnitName);
    }

    [Theory]
    [InlineData(eModelUnit.MicroMeter, 0.001)]
    [InlineData(eModelUnit.Foot, 304.8)]
    public void Import_ExoticUnits_NormalizeAndReportName(eModelUnit unit, double millimetersPerUnit)
    {
        byte[] file = ExportWithUnit(GoldenMeshes.Cube(), unit, millimetersPerUnit);

        MeshImportResult result = ThreeMfImporter.Import(file);

        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
        Assert.Equal(millimetersPerUnit, result.Report.MillimetersPerUnit, 6);
        Assert.Equal(unit.ToString(), result.Report.SourceUnitName);
    }

    [Fact]
    public void Import_ReportsLostFeatures()
    {
        byte[] file = BuildLossyModel();

        MeshImportResult result = ThreeMfImporter.Import(file);

        Assert.Equal(2, result.Report.TriangleCount);
        Assert.Contains("метаданные (Title, Designer и др.)", result.Report.LostFeatures);
        Assert.Contains("имена объектов (name)", result.Report.LostFeatures);
        Assert.Contains(result.Report.LostFeatures, f => f.Contains("объединены в один меш", StringComparison.Ordinal));
        Assert.Contains(result.Report.LostFeatures, f => f.Contains("components", StringComparison.Ordinal));
        Assert.Contains(result.Report.LostFeatures, f => f.Contains("UUID", StringComparison.Ordinal));
        Assert.Contains(result.Report.LostFeatures, f => f.Contains("property groups", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_AppliesBuildItemAndComponentTransforms()
    {
        byte[] file = BuildLossyModel();

        MeshImportResult result = ThreeMfImporter.Import(file);

        // Меш 1 (треугольник в начале координат) перенесён компонентом на (10, 0, 0),
        // затем трансформом build item на (0, 20, 0); меш 2 — только трансформом build item.
        Assert.Contains(result.Mesh.Positions, p => Math.Abs(p.X - 10) <= 0.01 && Math.Abs(p.Y - 20) <= 0.01);
        Assert.Contains(result.Mesh.Positions, p => Math.Abs(p.X - 1) <= 0.01 && Math.Abs(p.Y - 20) <= 0.01);
    }

    [Fact]
    public void Import_EmptyModel_Warns()
    {
        byte[] file = ThreeMfExporter.Export(new MeshData([], []));

        MeshImportResult result = ThreeMfImporter.Import(file);

        Assert.Equal(0, result.Report.TriangleCount);
        Assert.Contains(result.Report.Warnings, w => w.Contains("не содержит треугольников", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_OpenModel_WithoutBuildItems_Warns()
    {
        byte[] file = BuildObjectWithoutBuildItems();

        MeshImportResult result = ThreeMfImporter.Import(file);

        Assert.Equal(1, result.Report.TriangleCount);
        Assert.Contains(result.Report.Warnings, w => w.Contains("build items", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_DegenerateTriangle_ReportsIndex()
    {
        byte[] file = BuildDegenerateModel();

        MeshImportResult result = ThreeMfImporter.Import(file);

        Assert.Equal(2, result.Report.TriangleCount);
        Assert.Equal(1, result.Report.DegenerateTriangleCount);
        Assert.Contains(1, result.Report.DegenerateTriangleIndices);
    }

    [Fact]
    public void Import_Corrupt3Mf_ThrowsInvalidData()
    {
        byte[] file = ThreeMfExporter.Export(GoldenMeshes.Cube());
        byte[] truncated = file[..(file.Length / 2)];

        Assert.Throws<InvalidDataException>(() => ThreeMfImporter.Import(truncated));
    }

    [Fact]
    public void Export_InvalidIndices_Throws()
    {
        var bad = new MeshData([new Vector3d(0, 0, 0)], [0, 1, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => ThreeMfExporter.Export(bad));
        Assert.Throws<ArgumentException>(() => ThreeMfExporter.Export(new MeshData([new Vector3d(0, 0, 0)], [0])));
    }

    [Fact]
    public void Detect_Identifies3Mf()
    {
        Assert.Equal(MeshFileFormat.ThreeMf, MeshFile.Detect(ThreeMfExporter.Export(GoldenMeshes.Cube())));
    }

    [Fact]
    public void Detect_ZipWithoutModel_Throws()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open(), Encoding.UTF8);
            writer.Write("<Types/>");
        }

        Assert.Throws<InvalidDataException>(() => MeshFile.Detect(stream.ToArray()));
    }

    [Fact]
    public void MeshFile_DispatchesExportAndImport()
    {
        byte[] file = MeshFile.Export(GoldenMeshes.Cup(), MeshFileFormat.ThreeMf);

        Assert.Equal(ThreeMfExporter.Export(GoldenMeshes.Cup()), file);
        MeshImportResult result = MeshFile.Import(file);
        Assert.Equal(MeshFileFormat.ThreeMf, result.Report.Format);
        Assert.Equal(28, result.Report.TriangleCount);
    }

    private static MeshData GetMesh(string name) => name switch
    {
        "Cube" => GoldenMeshes.Cube(),
        "Cup" => GoldenMeshes.Cup(),
        "Stand" => GoldenMeshes.Stand(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static double SizeOn(MeshData mesh, Func<Vector3d, double> coordinate)
    {
        double min = mesh.Positions.Min(coordinate);
        double max = mesh.Positions.Max(coordinate);
        return max - min;
    }

    private static string ReadModelXml(byte[] file)
    {
        using var stream = new MemoryStream(file);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("3D/3dmodel.model")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Экспорт меша с единицами вне <see cref="LengthUnit"/> (микрометр, фут) напрямую через lib3mf.</summary>
    private static void AssertZipMatchesGolden(byte[] expected, byte[] actual)
    {
        using var expectedStream = new MemoryStream(expected);
        using var actualStream = new MemoryStream(actual);
        using var expectedZip = new ZipArchive(expectedStream, ZipArchiveMode.Read);
        using var actualZip = new ZipArchive(actualStream, ZipArchiveMode.Read);

        List<string> expectedNames = expectedZip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        List<string> actualNames = actualZip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(expectedNames, actualNames);

        foreach (string name in expectedNames)
        {
            byte[] expectedBytes = ReadEntry(expectedZip.GetEntry(name)!);
            byte[] actualBytes = ReadEntry(actualZip.GetEntry(name)!);
            bool isText = name.EndsWith(".model", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
            if (isText)
            {
                Assert.Equal(NormalizeNewLines(expectedBytes), NormalizeNewLines(actualBytes));
            }
            else
            {
                Assert.Equal(expectedBytes, actualBytes);
            }
        }
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string NormalizeNewLines(byte[] content) =>
        Encoding.UTF8.GetString(content).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static byte[] ExportWithUnit(MeshData mesh, eModelUnit unit, double millimetersPerUnit)
    {
        using CModel model = Wrapper.CreateModel();
        model.SetUnit(unit);
        using CMeshObject meshObject = model.AddMeshObject();
        foreach (Vector3d position in mesh.Positions)
        {
            meshObject.AddVertex(new sPosition
            {
                Coordinates = new[] { (float)(position.X / millimetersPerUnit), (float)(position.Y / millimetersPerUnit), (float)(position.Z / millimetersPerUnit) },
            });
        }

        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            meshObject.AddTriangle(new sTriangle
            {
                Indices = new[] { (uint)mesh.Indices[i], (uint)mesh.Indices[i + 1], (uint)mesh.Indices[i + 2] },
            });
        }

        using CBuildItem buildItem = model.AddBuildItem(meshObject, IdentityTransform());
        using CWriter writer = model.QueryWriter("3mf");
        writer.WriteToBuffer(out byte[] buffer);
        return buffer;
    }

    /// <summary>
    /// «Потерянная» модель: два именованных меша, метаданные, материал,
    /// сборка (components) с трансформациями и build item с собственным трансформом.
    /// </summary>
    private static byte[] BuildLossyModel()
    {
        using CModel model = Wrapper.CreateModel();
        using (CMetaDataGroup metaData = model.GetMetaDataGroup())
        {
            metaData.AddMetaData(string.Empty, "Title", "Потерянная модель", "xs:string", true);
        }

        using CBaseMaterialGroup materials = model.AddBaseMaterialGroup();
        materials.AddMaterial("PLA", new sColor { Red = 255, Green = 0, Blue = 0, Alpha = 255 });

        using CMeshObject mesh1 = model.AddMeshObject();
        mesh1.SetName("Треугольник 1");
        mesh1.AddVertex(new sPosition { Coordinates = new float[] { 0, 0, 0 } });
        mesh1.AddVertex(new sPosition { Coordinates = new float[] { 1, 0, 0 } });
        mesh1.AddVertex(new sPosition { Coordinates = new float[] { 0, 1, 0 } });
        mesh1.AddTriangle(new sTriangle { Indices = new uint[] { 0, 1, 2 } });

        using CMeshObject mesh2 = model.AddMeshObject();
        mesh2.SetName("Треугольник 2");
        mesh2.AddVertex(new sPosition { Coordinates = new float[] { 1, 0, 0 } });
        mesh2.AddVertex(new sPosition { Coordinates = new float[] { 2, 0, 0 } });
        mesh2.AddVertex(new sPosition { Coordinates = new float[] { 1, 1, 0 } });
        mesh2.AddTriangle(new sTriangle { Indices = new uint[] { 0, 1, 2 } });

        using CComponentsObject components = model.AddComponentsObject();
        components.AddComponent(mesh1, Translate(10, 0, 0));
        components.AddComponent(mesh2, IdentityTransform());

        using CBuildItem buildItem = model.AddBuildItem(components, Translate(0, 20, 0));
        using CWriter writer = model.QueryWriter("3mf");
        writer.WriteToBuffer(out byte[] buffer);
        return buffer;
    }

    /// <summary>Модель без build items: импортёр должен взять объекты модели и предупредить.</summary>
    private static byte[] BuildObjectWithoutBuildItems()
    {
        using CModel model = Wrapper.CreateModel();
        using CMeshObject meshObject = model.AddMeshObject();
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 0, 0, 0 } });
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 1, 0, 0 } });
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 0, 1, 0 } });
        meshObject.AddTriangle(new sTriangle { Indices = new uint[] { 0, 1, 2 } });

        using CWriter writer = model.QueryWriter("3mf");
        writer.WriteToBuffer(out byte[] buffer);
        return buffer;
    }

    /// <summary>Два треугольника, второй вырожден (три коллинеарные вершины — нулевая площадь).</summary>
    private static byte[] BuildDegenerateModel()
    {
        using CModel model = Wrapper.CreateModel();
        using CMeshObject meshObject = model.AddMeshObject();
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 0, 0, 0 } });
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 1, 0, 0 } });
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 0, 1, 0 } });
        meshObject.AddVertex(new sPosition { Coordinates = new float[] { 2, 0, 0 } });
        meshObject.AddTriangle(new sTriangle { Indices = new uint[] { 0, 1, 2 } });
        meshObject.AddTriangle(new sTriangle { Indices = new uint[] { 0, 1, 3 } });

        using CBuildItem buildItem = model.AddBuildItem(meshObject, IdentityTransform());
        using CWriter writer = model.QueryWriter("3mf");
        writer.WriteToBuffer(out byte[] buffer);
        return buffer;
    }

    private static sTransform IdentityTransform() => new()
    {
        Fields = new float[][]
        {
            new float[] { 1, 0, 0 },
            new float[] { 0, 1, 0 },
            new float[] { 0, 0, 1 },
            new float[] { 0, 0, 0 },
        },
    };

    private static sTransform Translate(float x, float y, float z) => new()
    {
        Fields = new float[][]
        {
            new float[] { 1, 0, 0 },
            new float[] { 0, 1, 0 },
            new float[] { 0, 0, 1 },
            new float[] { x, y, z },
        },
    };
}
