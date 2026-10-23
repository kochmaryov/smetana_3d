using SmetaNA.Core.Geometry;
using SmetaNA.Formats.Validation;
using Xunit;

namespace SmetaNA.Formats.Tests.ReferenceModels;

/// <summary>
/// Эталонные модели (задача 4.6, DoD §5.3): замкнутость/печатаемость,
/// round-trip экспорт→импорт во всех форматах с точностью 0,01 мм (DoD §5.4)
/// и сверка закоммиченных фикстур для слайсеров.
/// </summary>
public sealed class ReferenceModelExportTests
{
    public static TheoryData<string> ModelNames()
    {
        var data = new TheoryData<string>();
        foreach (string name in ReferenceModelFixtures.ModelNames)
        {
            data.Add(name);
        }

        return data;
    }

    public static TheoryData<string, MeshFileFormat> ModelFormats()
    {
        var data = new TheoryData<string, MeshFileFormat>();
        foreach (string name in ReferenceModelFixtures.ModelNames)
        {
            foreach (MeshFileFormat format in ReferenceModelFixtures.Formats)
            {
                data.Add(name, format);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModelNames))]
    public void ReferenceModel_IsWatertightPrintableSolid(string modelName)
    {
        MeshData mesh = ReferenceModelFixtures.Build(modelName);
        HalfEdgeMesh halfEdge = HalfEdgeMesh.FromTriangles(mesh.Positions, mesh.Indices);

        var validation = MeshValidator.Validate(halfEdge);
        Assert.True(validation.IsValid, $"{modelName}: {string.Join("; ", validation.Defects)}");
        Assert.True(validation.IsWatertight, modelName);
        Assert.True(halfEdge.IsWatertight, modelName);

        var report = PrintabilityValidator.Validate(mesh);
        Assert.True(report.IsPrintable, report.ToText());
        Assert.True(report.IsOutwardOriented, modelName);

        double volume = MeshAssert.SignedVolume(mesh);
        Assert.True(volume > 0, $"{modelName}: ориентированный объём {volume} мм³");
    }

    [Theory]
    [MemberData(nameof(ModelNames))]
    public void ReferenceModel_VolumeMatchesAnalyticFormula(string modelName)
    {
        MeshData mesh = ReferenceModelFixtures.Build(modelName);
        double actual = MeshAssert.SignedVolume(mesh);

        // Аналитический объём: полигональная аппроксимация круга (48 сегментов).
        double expected = modelName switch
        {
            "cup" => (PolygonArea(48, 30) * 80) - (PolygonArea(48, 25) * 75),
            "stand" => (100 * 60 * 20) + (60 * 60 * 30),
            "enclosure" => (100 * 80 * 40) - (92 * 72 * 36) - (24 * 4 * 12),
            _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, null),
        };

        Assert.True(
            Math.Abs(actual - expected) <= expected * 0.001,
            $"{modelName}: объём {actual:F1} мм³ вместо {expected:F1} мм³");
    }

    [Theory]
    [MemberData(nameof(ModelFormats))]
    public void ExportRoundTrip_KeepsGeometryWithinDodTolerance(string modelName, MeshFileFormat format)
    {
        MeshData mesh = ReferenceModelFixtures.Build(modelName);

        byte[] bytes = MeshFile.Export(mesh, format);
        MeshImportResult result = MeshFile.Import(bytes);

        MeshAssert.EqualCorners(mesh, result.Mesh, 0.01);
        double expectedVolume = MeshAssert.SignedVolume(mesh);
        double actualVolume = MeshAssert.SignedVolume(result.Mesh);
        Assert.True(
            Math.Abs(actualVolume - expectedVolume) <= expectedVolume * 0.01,
            $"{modelName}/{format}: объём {actualVolume:F1} вместо {expectedVolume:F1} мм³");
    }

    [Theory]
    [MemberData(nameof(ModelFormats))]
    public void CommittedFixture_MatchesFactoryModel(string modelName, MeshFileFormat format)
    {
        string path = ReferenceModelFixtures.OutputPath(ReferenceModelFixtures.FileName(modelName, format));
        Assert.True(
            File.Exists(path),
            $"Фикстура {path} не найдена; регенерация: dotnet test с SMETANA_REGEN_FIXTURES=1.");

        MeshData mesh = ReferenceModelFixtures.Build(modelName);
        MeshImportResult result = MeshFile.Import(File.ReadAllBytes(path));
        MeshAssert.EqualCorners(mesh, result.Mesh, 0.01);
    }

    /// <summary>Площадь правильного N-угольника описанной окружности радиуса R, мм².</summary>
    private static double PolygonArea(int segments, double radius) =>
        0.5 * segments * radius * radius * Math.Sin((2.0 * Math.PI) / segments);
}
