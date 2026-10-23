using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.Stl;
using Xunit;

namespace SmetaNA.Formats.Tests;

public sealed class StlImportTests
{
    [Fact]
    public void Import_BinaryFixture_DetectsBinaryAndWeldsCube()
    {
        MeshImportResult result = StlImporter.Import(Fixtures.Read("cube.bin.stl"));

        Assert.Equal(MeshFileFormat.BinaryStl, result.Report.Format);
        Assert.Equal(12, result.Report.TriangleCount);
        Assert.Equal(8, result.Report.VertexCount);
        Assert.Equal(36, result.Report.SourceVertexCount);
        Assert.Equal(28, result.Report.WeldedVertexCount);
        Assert.Empty(result.Report.DegenerateTriangleIndices);
    }

    [Fact]
    public void Import_AsciiFixture_DetectsAscii()
    {
        MeshImportResult result = StlImporter.Import(Fixtures.Read("cube.ascii.stl"));

        Assert.Equal(MeshFileFormat.AsciiStl, result.Report.Format);
        Assert.Equal(12, result.Report.TriangleCount);
        Assert.Equal(8, result.Report.VertexCount);
    }

    [Fact]
    public void Import_BinaryWithSolidHeader_IsStillBinary()
    {
        byte[] bytes = Fixtures.Read("cube.bin.stl");
        Encoding.ASCII.GetBytes("solid fake").CopyTo(bytes, 0);

        MeshImportResult result = StlImporter.Import(bytes);

        Assert.Equal(MeshFileFormat.BinaryStl, result.Report.Format);
        Assert.Equal(12, result.Report.TriangleCount);
    }

    [Fact]
    public void Import_ExternalAsciiFixture_TolerantParsing()
    {
        MeshImportResult result = StlImporter.Import(Fixtures.Read("external-ascii.stl"));

        Assert.Equal(MeshFileFormat.AsciiStl, result.Report.Format);
        Assert.Equal(2, result.Report.TriangleCount);
        Assert.Equal(4, result.Report.VertexCount);
        Assert.Contains(result.Mesh.Positions, p => p.X == -15 && p.Y == 15 && p.Z == 0);
    }

    [Fact]
    public void Import_AsciiWithCrLfAndBlankLines_Parses()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(
            "solid t\r\n\r\nfacet normal 0 0 1\r\n\touter loop\r\n" +
            "vertex 0 0 0\r\nvertex 2 0 0\r\nvertex 0 2 0\r\n" +
            "endloop\r\nendfacet\r\nendsolid t\r\n");

        MeshImportResult result = StlImporter.Import(bytes);

        Assert.Equal(1, result.Report.TriangleCount);
    }

    [Fact]
    public void Import_EmptyAsciiMesh_Warns()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("solid empty\nendsolid empty\n");

        MeshImportResult result = StlImporter.Import(bytes);

        Assert.Equal(0, result.Report.TriangleCount);
        Assert.Contains(result.Report.Warnings, w => w.Contains("не содержит треугольников", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_UnitInch_NormalizesToMillimeters()
    {
        byte[] file = StlExporter.ExportBinary(GoldenMeshes.Cube(), new MeshExportOptions(LengthUnit.Inch));

        MeshImportResult result = StlImporter.Import(file, new MeshImportOptions(LengthUnit.Inch));

        Assert.Equal(25.4, result.Report.MillimetersPerUnit);
        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
    }

    [Fact]
    public void Import_ReportListsLostFeatures()
    {
        MeshImportResult result = StlImporter.Import(Fixtures.Read("cup.bin.stl"));

        Assert.Contains("материалы и цвета (STL не хранит)", result.Report.LostFeatures);
        Assert.Contains("имена объектов, группы и структура сцены (STL не хранит)", result.Report.LostFeatures);
        Assert.Contains("нормали из файла (пересчитываются по геометрии)", result.Report.LostFeatures);
        Assert.Contains("Формат: BinaryStl", result.Report.ToText());
    }

    [Fact]
    public void Import_DegenerateTriangle_IsReported()
    {
        var mesh = new MeshData(
            [new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(2, 0, 0)],
            [0, 1, 2]);
        byte[] file = StlExporter.ExportBinary(mesh);

        MeshImportResult result = StlImporter.Import(file);

        Assert.Equal(1, result.Report.DegenerateTriangleCount);
        Assert.Contains(0, result.Report.DegenerateTriangleIndices);
        Assert.Contains(result.Report.Warnings, w => w.Contains("Вырожденных треугольников", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_WeldingDisabled_KeepsTriangleSoup()
    {
        MeshImportResult result = StlImporter.Import(
            Fixtures.Read("cube.bin.stl"),
            new MeshImportOptions(WeldVertices: false));

        Assert.Equal(36, result.Report.VertexCount);
        Assert.Equal(0, result.Report.WeldedVertexCount);
    }

    [Fact]
    public void Import_TruncatedBinary_Throws()
    {
        byte[] bytes = Fixtures.Read("cube.bin.stl");
        byte[] truncated = bytes.AsSpan(0, bytes.Length - 10).ToArray();

        Assert.Throws<InvalidDataException>(() => StlImporter.Import(truncated));
    }

    [Fact]
    public void Import_UnknownBytes_Throws()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("this is not a mesh file at all, really");

        Assert.Throws<InvalidDataException>(() => StlImporter.Import(bytes));
    }

    [Fact]
    public void Import_AsciiBrokenVertexCount_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("solid t\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nendloop\nendfacet\nendsolid t\n");

        Assert.Throws<InvalidDataException>(() => StlImporter.Import(bytes));
    }

    [Fact]
    public void Import_AsciiBrokenNumber_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("solid t\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1,5 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid t\n");

        Assert.Throws<InvalidDataException>(() => StlImporter.Import(bytes));
    }
}
