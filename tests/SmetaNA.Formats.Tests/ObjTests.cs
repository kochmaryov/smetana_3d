using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.Obj;
using Xunit;

namespace SmetaNA.Formats.Tests;

public sealed class ObjTests
{
    [Fact]
    public void Export_MatchesGoldenFixture_Cube()
    {
        MeshAssert.EqualNormalizedText(Fixtures.Read("cube.obj"), ObjExporter.Export(GoldenMeshes.Cube()));
    }

    [Fact]
    public void Export_IsByteStable()
    {
        Assert.Equal(ObjExporter.Export(GoldenMeshes.Stand()), ObjExporter.Export(GoldenMeshes.Stand()));
    }

    [Fact]
    public void Export_DeclaresUnitsAndWritesOneBasedFaces()
    {
        string text = Encoding.UTF8.GetString(ObjExporter.Export(GoldenMeshes.Cube()));

        Assert.StartsWith("# SmetaNA OBJ export\n# units: Millimeter (1 unit = 1 mm)\no SmetaNA\n", text);
        Assert.Equal(8, CountOccurrences(text, "\nv ") + (text.StartsWith("v ", StringComparison.Ordinal) ? 1 : 0));
        Assert.Equal(12, CountOccurrences(text, "\nf ") + (text.StartsWith("f ", StringComparison.Ordinal) ? 1 : 0));
        Assert.Contains("f 1 3 2", text);
        Assert.DoesNotContain("f 0", text);
    }

    [Fact]
    public void Export_UnitInch_ScalesAndDeclaresUnits()
    {
        string text = Encoding.UTF8.GetString(ObjExporter.Export(
            GoldenMeshes.Cube(),
            new MeshExportOptions(LengthUnit.Inch)));

        Assert.Contains("# units: Inch (1 unit = 25.4 mm)", text);
        Assert.Contains("v 3.937008 0 0", text);
    }

    [Fact]
    public void Export_EmptyMesh_WritesNoFaces()
    {
        string text = Encoding.UTF8.GetString(ObjExporter.Export(new MeshData([], [])));

        Assert.DoesNotContain("\nf ", text);
    }

    [Fact]
    public void Import_QuadAndNegativeIndices_Triangulates()
    {
        MeshImportResult result = ObjImporter.Import(Fixtures.Read("lossy.obj"));

        Assert.Equal(MeshFileFormat.Obj, result.Report.Format);
        Assert.Equal(3, result.Report.TriangleCount);
        Assert.Contains(result.Mesh.Positions, p => p.X == 5 && p.Y == 5 && p.Z == 5);
    }

    [Fact]
    public void Import_LossyObj_ReportsLostFeatures()
    {
        MeshImportResult result = ObjImporter.Import(Fixtures.Read("lossy.obj"));

        Assert.Contains("материалы (mtllib/usemtl)", result.Report.LostFeatures);
        Assert.Contains("текстурные координаты (vt)", result.Report.LostFeatures);
        Assert.Contains("нормали (vn) — пересчитываются по геометрии", result.Report.LostFeatures);
        Assert.Contains("имена объектов и группы (o/g)", result.Report.LostFeatures);
        Assert.Contains("сглаживание (s)", result.Report.LostFeatures);
        Assert.Contains("линии и точки (l/p)", result.Report.LostFeatures);
        Assert.Contains(result.Report.Warnings, w => w.Contains("Полигоны", StringComparison.Ordinal));
        Assert.Contains(result.Report.Warnings, w => w.Contains("веером", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_UnitInch_NormalizesToMillimeters()
    {
        byte[] file = ObjExporter.Export(GoldenMeshes.Cube(), new MeshExportOptions(LengthUnit.Inch));

        MeshImportResult result = ObjImporter.Import(file, new MeshImportOptions(LengthUnit.Inch));

        MeshAssert.EqualCorners(GoldenMeshes.Cube(), result.Mesh, 0.01);
    }

    [Fact]
    public void Import_UnknownKeyword_Warns()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("bevel 1\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        MeshImportResult result = ObjImporter.Import(bytes);

        Assert.Contains(result.Report.Warnings, w => w.Contains("bevel", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_IndexOutOfRange_Throws()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 4\n");

        Assert.Throws<InvalidDataException>(() => ObjImporter.Import(bytes));
    }

    [Fact]
    public void Import_EmptyObj_Warns()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("# пустой файл\n");

        MeshImportResult result = ObjImporter.Import(bytes);

        Assert.Equal(0, result.Report.TriangleCount);
        Assert.Contains(result.Report.Warnings, w => w.Contains("не содержит треугольников", StringComparison.Ordinal));
    }

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
