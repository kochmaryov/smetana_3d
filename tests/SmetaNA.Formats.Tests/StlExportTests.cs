using System.Buffers.Binary;
using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.Stl;
using Xunit;

namespace SmetaNA.Formats.Tests;

public sealed class StlExportTests
{
    [Fact]
    public void ExportBinary_Cube_HasStlStructure()
    {
        byte[] bytes = StlExporter.ExportBinary(GoldenMeshes.Cube());

        Assert.Equal(84 + (50 * 12), bytes.Length);
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80, 4)));
        Assert.StartsWith("SmetaNA binary STL; units: Millimeter", Encoding.ASCII.GetString(bytes, 0, 80));
        Assert.Equal(0f, ReadSingle(bytes, 84 + 0));
        Assert.Equal(0f, ReadSingle(bytes, 84 + 4));
        Assert.Equal(-1f, ReadSingle(bytes, 84 + 8));
    }

    [Fact]
    public void ExportBinary_IsByteStable()
    {
        byte[] first = StlExporter.ExportBinary(GoldenMeshes.Cup());
        byte[] second = StlExporter.ExportBinary(GoldenMeshes.Cup());

        Assert.Equal(first, second);
    }

    [Fact]
    public void ExportBinary_MatchesGoldenFixture_Cube()
    {
        Assert.Equal(Fixtures.Read("cube.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Cube()));
    }

    [Fact]
    public void ExportBinary_MatchesGoldenFixture_Cup()
    {
        Assert.Equal(Fixtures.Read("cup.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Cup()));
    }

    [Fact]
    public void ExportBinary_MatchesGoldenFixture_Stand()
    {
        Assert.Equal(Fixtures.Read("stand.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Stand()));
    }

    [Fact]
    public void ExportAscii_MatchesGoldenFixture_Cube()
    {
        MeshAssert.EqualNormalizedText(Fixtures.Read("cube.ascii.stl"), StlExporter.ExportAscii(GoldenMeshes.Cube()));
    }

    [Fact]
    public void ExportAscii_UsesInvariantCultureAndStructure()
    {
        string text = Encoding.UTF8.GetString(StlExporter.ExportAscii(GoldenMeshes.Cube()));

        Assert.StartsWith("solid SmetaNA\n", text);
        Assert.EndsWith("endsolid SmetaNA\n", text);
        Assert.Equal(12, CountOccurrences(text, "facet normal"));
        Assert.Equal(36, CountOccurrences(text, "vertex"));
        Assert.DoesNotContain(',', text);
    }

    [Fact]
    public void ExportBinary_UnitInch_ScalesCoordinates()
    {
        byte[] bytes = StlExporter.ExportBinary(
            GoldenMeshes.Cube(),
            new MeshExportOptions(LengthUnit.Inch));

        Assert.Equal(0f, ReadSingle(bytes, 84 + 12));
        Assert.Equal(0f, ReadSingle(bytes, 84 + 16));
        Assert.Equal(0f, ReadSingle(bytes, 84 + 20));
        Assert.Equal((float)(100 / 25.4), ReadSingle(bytes, 84 + 24));
    }

    [Fact]
    public void ExportAscii_UnitMeter_ScalesCoordinates()
    {
        string text = Encoding.UTF8.GetString(StlExporter.ExportAscii(
            GoldenMeshes.Cube(),
            new MeshExportOptions(LengthUnit.Meter)));

        Assert.Contains("vertex 0.1 0 0", text);
    }

    [Fact]
    public void ExportBinary_FromHalfEdgeMesh_MatchesMeshData()
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.FromTriangles(
            [.. GoldenMeshes.Cube().Positions],
            [.. GoldenMeshes.Cube().Indices]);

        Assert.Equal(
            StlExporter.ExportBinary(GoldenMeshes.Cube()),
            StlExporter.ExportBinary(mesh));
    }

    [Fact]
    public void ExportAscii_ModelName_IsSanitized()
    {
        string text = Encoding.UTF8.GetString(StlExporter.ExportAscii(
            new MeshData([new Vector3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)], [0, 1, 2]),
            new MeshExportOptions(ModelName: "Деталь\nс переносом")));

        Assert.StartsWith("solid Деталь с переносом\n", text);
        Assert.EndsWith("endsolid Деталь с переносом\n", text);
    }

    [Fact]
    public void ExportBinary_EmptyMesh_WritesZeroTriangles()
    {
        byte[] bytes = StlExporter.ExportBinary(new MeshData([], []));

        Assert.Equal(84, bytes.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80, 4)));
    }

    [Fact]
    public void ExportBinary_IndexOutOfRange_Throws()
    {
        var mesh = new MeshData([new Vector3d(0, 0, 0)], [0, 1, 2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => StlExporter.ExportBinary(mesh));
    }

    [Fact]
    public void ExportBinary_IndicesNotMultipleOfThree_Throws()
    {
        var mesh = new MeshData([new Vector3d(0, 0, 0), new Vector3d(1, 0, 0)], [0, 1]);

        Assert.Throws<ArgumentException>(() => StlExporter.ExportBinary(mesh));
    }

    private static float ReadSingle(byte[] data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
