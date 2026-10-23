using System.IO.Compression;
using System.Text;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Round-trip формата проекта (задача 4.1): иерархия, группы, компоненты
/// (definition + instance, один definition — много instance), трансформации,
/// имена, видимость, единицы, вид, материалы; double — бит-в-бит.
/// </summary>
public sealed class ProjectFileRoundTripTests
{
    [Fact]
    public void SaveLoad_ComplexScene_PreservesStructureNamesVisibilityAndIds()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectLoadResult result = ProjectFileStore.Load(path);

        Assert.False(result.Migrated);
        Assert.Equal(ProjectFileFormat.CurrentSchemaVersion, result.SourceSchemaVersion);
        ProjectAssert.DocumentsEqual(expected, result.Document);

        SceneNode assembly = ProjectScenes.Require(result.Document.Scene, "Сборка");
        Assert.Equal(expected.Scene.Root.Children[0].Id, assembly.Id);
        Assert.Equal(3, assembly.Children.Count);

        SceneNode hidden = ProjectScenes.Require(result.Document.Scene, "Скрытое");
        Assert.False(hidden.IsVisible);
    }

    [Fact]
    public void SaveLoad_ComponentInstances_ShareSingleDefinition_AndNest()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        var leg1 = Assert.IsType<ComponentInstanceNode>(ProjectScenes.Require(actual.Scene, "Ножка №1"));
        var leg2 = Assert.IsType<ComponentInstanceNode>(ProjectScenes.Require(actual.Scene, "Ножка №2"));

        // Один definition — много instance: после загрузки определение общее.
        Assert.Same(leg1.Definition, leg2.Definition);
        Assert.Equal("Ножка", leg1.Definition.Name);

        // Вложенный компонент: внутри «Ножки» — инстанс «Шляпки» со своим definition.
        var legContent = Assert.IsType<GroupNode>(leg1.Definition.Content);
        var capInstance = Assert.IsType<ComponentInstanceNode>(
            legContent.Children.Single(n => n.Name == "Шляпка №1"));
        Assert.Equal("Шляпка", capInstance.Definition.Name);
        var capContent = Assert.IsType<MeshNode>(capInstance.Definition.Content);
        Assert.Equal("Шляпка", capContent.Name);
    }

    [Fact]
    public void SaveLoad_MeshGeometryAndTransforms_AreBitExact()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        var assembly = Assert.IsType<GroupNode>(ProjectScenes.Require(actual.Scene, "Сборка"));
        ProjectAssert.DoubleBits(Math.PI, assembly.LocalTransform.M11, "transform.m11");
        ProjectAssert.DoubleBits(5e-324, assembly.LocalTransform.M13, "transform.m13 (денормализованная)");
        ProjectAssert.DoubleBits(1e-300, assembly.LocalTransform.M23, "transform.m23");
        ProjectAssert.DoubleBits(-0.0, assembly.LocalTransform.M21, "transform.m21 (минус-ноль)");
        ProjectAssert.DoubleBits(1.7976931348623157E+308, assembly.LocalTransform.M33, "transform.m33");

        var detail = Assert.IsType<MeshNode>(ProjectScenes.Require(actual.Scene, "Деталь А"));
        var expectedDetail = (MeshNode)ProjectScenes.Require(expected.Scene, "Деталь А");
        for (int i = 0; i < expectedDetail.Mesh.Vertices.Count; i++)
        {
            Vector3d e = expectedDetail.Mesh.Vertices[i].Position;
            Vector3d a = detail.Mesh.Vertices[i].Position;
            ProjectAssert.DoubleBits(e.X, a.X, $"vertex[{i}].x");
            ProjectAssert.DoubleBits(e.Y, a.Y, $"vertex[{i}].y");
            ProjectAssert.DoubleBits(e.Z, a.Z, $"vertex[{i}].z");
        }

        // Полигональная топология (кольца граней) сохраняется, а не пересобирается в треугольники.
        Assert.Equal(expectedDetail.Mesh.Faces.Count, detail.Mesh.Faces.Count);
    }

    [Fact]
    public void SaveLoad_SharedMeshInstance_StaysShared()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        var detail = Assert.IsType<MeshNode>(ProjectScenes.Require(actual.Scene, "Деталь А"));
        var shared = Assert.IsType<MeshNode>(ProjectScenes.Require(actual.Scene, "Общий меш"));
        Assert.Same(detail.Mesh, shared.Mesh);
    }

    [Fact]
    public void SaveLoad_UnitsViewAndMaterials_AreExplicitAndBitExact()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        expected.View = new ProjectViewSettings
        {
            Projection = ProjectProjectionMode.Orthographic,
            Target = new Vector3d(0.1, Math.PI, -0.25),
            Distance = 1234.5678901234567,
            Yaw = -2.5,
            Pitch = 0.3,
            FieldOfViewY = 0.7,
            OrthoHeight = 250.125,
            AspectRatio = 1.6,
            NearPlane = 0.75,
            FarPlane = 999999.5,
        };
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectLoadResult result = ProjectFileStore.Load(path);

        ProjectAssert.DocumentsEqual(expected, result.Document);
        Assert.Equal(LengthUnit.Centimeter, result.Document.Unit);
        Assert.Equal(ProjectProjectionMode.Orthographic, result.Document.View.Projection);
        Assert.Single(result.Document.Materials);
        Assert.Equal("Красный", result.Document.Materials[0].Name);
    }

    [Fact]
    public void SaveLoad_EmptyScene_RoundTrips()
    {
        using var temp = new TempDirectory();
        var expected = new ProjectDocument { Unit = LengthUnit.Meter };
        string path = temp.FilePath("empty" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, expected);
        ProjectDocument actual = ProjectFileStore.Load(path).Document;

        Assert.Empty(actual.Scene.Root.Children);
        Assert.Equal(LengthUnit.Meter, actual.Unit);
        Assert.Empty(actual.Materials);
    }

    [Fact]
    public void Save_IsByteStable()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string first = temp.FilePath("first" + ProjectFileFormat.Extension);
        string second = temp.FilePath("second" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(first, document);
        ProjectFileStore.Save(second, document);

        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    [Fact]
    public void Save_WritesBinaryMeshAssets_AndExplicitSchema()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectFileStore.Save(path, document);

        using var archive = ZipFile.OpenRead(path);
        Assert.NotNull(archive.GetEntry(ProjectFileFormat.ManifestEntryName));

        ZipArchiveEntry asset = archive.GetEntry(ProjectFileFormat.MeshAssetsDirectory + "m0.bin")
            ?? throw new InvalidOperationException("Ассет m0.bin не найден.");
        using Stream assetStream = asset.Open();
        using var buffer = new MemoryStream();
        assetStream.CopyTo(buffer);
        byte[] assetBytes = buffer.ToArray();

        // Меши — бинарно (double-позиции), а не текстом.
        Assert.True(assetBytes.Length > 16, "ассет слишком мал");
        Assert.Equal("SNAM", Encoding.ASCII.GetString(assetBytes, 0, 4));
        Assert.Contains((byte)0, assetBytes);

        string manifest;
        using (Stream manifestStream = archive.GetEntry(ProjectFileFormat.ManifestEntryName)!.Open())
        using (var reader = new StreamReader(manifestStream, Encoding.UTF8))
        {
            manifest = reader.ReadToEnd();
        }

        Assert.Contains("\"format\": \"SmetaNA.Project\"", manifest);
        Assert.Contains("\"schemaVersion\": 1", manifest);
        Assert.Contains("\"units\": \"centimeter\"", manifest);
        Assert.Contains("\"assets/meshes/m0.bin\"", manifest);
    }

    [Fact]
    public void Load_TamperedMeshAsset_FailsChecksum()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, document);

        byte[] tampered = ProjectZip.CorruptEntry(File.ReadAllBytes(path), ProjectFileFormat.MeshAssetsDirectory + "m0.bin");
        var ex = Assert.Throws<ProjectFileException>(() => ProjectFileStore.Load(tampered));
        Assert.Contains("Контрольная сумма", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsProjectFile_DetectsProjectAndRejectsForeignData()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, ProjectScenes.ComplexDocument());

        Assert.True(ProjectFileStore.IsProjectFile(path));
        Assert.False(ProjectFileStore.IsProjectFile([1, 2, 3, 4, 5]));
        Assert.False(ProjectFileStore.IsProjectFile(Fixtures.Read("cube.obj")));
        Assert.False(ProjectFileStore.IsProjectFile(Fixtures.Read("cube.bin.stl")));
        Assert.False(ProjectFileStore.IsProjectFile(Fixtures.Read("cube.3mf")));
    }

    [Fact]
    public void Load_NotAProject_Throws()
    {
        Assert.Throws<ProjectFileException>(() => ProjectFileStore.Load([0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0]));
    }
}
