using SmetaNA.Formats.Obj;
using SmetaNA.Formats.Stl;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>Фасад MeshFile: авто-детект формата по «ключам» и диспетчеризация экспорта.</summary>
public sealed class MeshFileTests
{
    [Theory]
    [InlineData("cube.bin.stl", MeshFileFormat.BinaryStl)]
    [InlineData("cup.bin.stl", MeshFileFormat.BinaryStl)]
    [InlineData("stand.bin.stl", MeshFileFormat.BinaryStl)]
    [InlineData("cube.ascii.stl", MeshFileFormat.AsciiStl)]
    [InlineData("external-ascii.stl", MeshFileFormat.AsciiStl)]
    [InlineData("cube.obj", MeshFileFormat.Obj)]
    [InlineData("lossy.obj", MeshFileFormat.Obj)]
    public void Detect_IdentifiesFormatFromFixture(string fixture, MeshFileFormat expected)
    {
        Assert.Equal(expected, MeshFile.Detect(Fixtures.Read(fixture)));
    }

    [Theory]
    [InlineData("cube.bin.stl", MeshFileFormat.BinaryStl)]
    [InlineData("cube.ascii.stl", MeshFileFormat.AsciiStl)]
    [InlineData("cube.obj", MeshFileFormat.Obj)]
    public void Import_AutoDetectsFixtureFormat(string fixture, MeshFileFormat expected)
    {
        MeshImportResult result = MeshFile.Import(Fixtures.Read(fixture));

        Assert.Equal(expected, result.Report.Format);
        Assert.Equal(12, result.Report.TriangleCount);
    }

    [Fact]
    public void Detect_Stream_PreservesPosition()
    {
        byte[] bytes = Fixtures.Read("cube.obj");
        using var stream = new MemoryStream(bytes);

        Assert.Equal(MeshFileFormat.Obj, MeshFile.Detect(stream));
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(MeshFileFormat.BinaryStl)]
    [InlineData(MeshFileFormat.AsciiStl)]
    [InlineData(MeshFileFormat.Obj)]
    public void Export_DispatchesToFormatExporter(MeshFileFormat format)
    {
        byte[] expected = format switch
        {
            MeshFileFormat.BinaryStl => StlExporter.ExportBinary(GoldenMeshes.Cup()),
            MeshFileFormat.AsciiStl => StlExporter.ExportAscii(GoldenMeshes.Cup()),
            _ => ObjExporter.Export(GoldenMeshes.Cup()),
        };

        Assert.Equal(expected, MeshFile.Export(GoldenMeshes.Cup(), format));
    }

    [Fact]
    public void Import_UnknownFormat_Throws()
    {
        byte[] bytes = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A];

        Assert.Throws<InvalidDataException>(() => MeshFile.Import(bytes));
    }
}
