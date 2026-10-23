using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using SmetaNA.Formats.ProjectFile.Templates;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Шаблоны проектов (задача 4.1): «пусто в мм», «стакан», «подставка» —
/// единицы, стартовые сцены и их прохождение через формат файла.
/// </summary>
public sealed class ProjectTemplateTests
{
    [Fact]
    public void Catalog_ContainsBuiltInTemplates()
    {
        Assert.Equal(3, ProjectTemplates.All.Count);
        Assert.NotNull(ProjectTemplates.Find(ProjectTemplates.EmptyMillimeterId));
        Assert.NotNull(ProjectTemplates.Find(ProjectTemplates.CupId));
        Assert.NotNull(ProjectTemplates.Find(ProjectTemplates.StandId));
    }

    [Fact]
    public void EmptyMillimeterTemplate_HasExplicitUnitsAndIsoView()
    {
        ProjectDocument document = ProjectTemplates.CreateDocument(ProjectTemplates.EmptyMillimeterId);

        Assert.Equal(LengthUnit.Millimeter, document.Unit);
        Assert.Empty(document.Scene.Root.Children);
        Assert.Equal(ProjectProjectionMode.Perspective, document.View.Projection);
    }

    [Fact]
    public void CupTemplate_IsWatertightAndMatchesReferenceVolume()
    {
        ProjectDocument document = ProjectTemplates.CreateDocument(ProjectTemplates.CupId);

        var cup = Assert.IsType<MeshNode>(Assert.Single(document.Scene.Root.Children));
        Assert.Equal("Стакан", cup.Name);
        Assert.True(cup.Mesh.IsWatertight, "меш стакана должен быть замкнутым");

        double volume = MeshAssert.SignedVolume(cup.Mesh.ToTriangles());
        Assert.True(
            Math.Abs(volume - GoldenMeshes.CupVolumeMm3) < 1e-6,
            $"объём стакана {volume} != {GoldenMeshes.CupVolumeMm3}");
    }

    [Fact]
    public void StandTemplate_IsWatertightAndMatchesReferenceVolume()
    {
        ProjectDocument document = ProjectTemplates.CreateDocument(ProjectTemplates.StandId);

        var stand = Assert.IsType<MeshNode>(Assert.Single(document.Scene.Root.Children));
        Assert.Equal("Подставка", stand.Name);
        Assert.True(stand.Mesh.IsWatertight, "меш подставки должен быть замкнутым");

        double volume = MeshAssert.SignedVolume(stand.Mesh.ToTriangles());
        Assert.True(
            Math.Abs(volume - GoldenMeshes.StandVolumeMm3) < 1e-6,
            $"объём подставки {volume} != {GoldenMeshes.StandVolumeMm3}");
    }

    [Fact]
    public void Templates_RoundTripThroughProjectFile()
    {
        using var temp = new TempDirectory();
        foreach (ProjectTemplate template in ProjectTemplates.All)
        {
            ProjectDocument expected = template.CreateDocument();
            string path = temp.FilePath(template.Id + ProjectFileFormat.Extension);

            ProjectFileStore.Save(path, expected);
            ProjectLoadResult result = ProjectFileStore.Load(path);

            Assert.False(result.Migrated);
            ProjectAssert.DocumentsEqual(expected, result.Document);
            Assert.Equal(LengthUnit.Millimeter, result.Document.Unit);
        }
    }

    [Fact]
    public void CreateDocument_EachTimeBuildsFreshScene()
    {
        ProjectDocument first = ProjectTemplates.CreateDocument(ProjectTemplates.CupId);
        ProjectDocument second = ProjectTemplates.CreateDocument(ProjectTemplates.CupId);

        Assert.NotEqual(
            ProjectScenes.Require(first.Scene, "Стакан").Id,
            ProjectScenes.Require(second.Scene, "Стакан").Id);
    }

    [Fact]
    public void UnknownTemplate_IsRejected()
    {
        Assert.Null(ProjectTemplates.Find("нет-такого"));
        Assert.Throws<ArgumentException>(() => ProjectTemplates.CreateDocument("нет-такого"));
    }
}
