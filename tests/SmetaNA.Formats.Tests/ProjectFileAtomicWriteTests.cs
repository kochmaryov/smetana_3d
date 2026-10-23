using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Атомарность записи формата проекта (задача 4.1): временный файл + замена,
/// имитация сбоя в середине записи — прежний файл не портится, «хвостов» не остаётся.
/// </summary>
public sealed class ProjectFileAtomicWriteTests
{
    [Fact]
    public void AtomicWrite_FailureMidWrite_KeepsPreviousFile_AndCleansTemp()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("data.bin");
        File.WriteAllBytes(path, [1, 2, 3, 4]);

        Assert.Throws<IOException>(() =>
            AtomicFileWriter.Write(path, stream =>
            {
                stream.Write(new byte[] { 9, 9, 9 });
                throw new IOException("Имитация сбоя записи (обрыв питания).");
            }));

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(path));
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void Save_CrashMidWrite_KeepsPreviousProjectIntact()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, document);
        byte[] savedBytes = File.ReadAllBytes(path);

        // Сбой в середине записи нового содержимого — через реальный код сохранения.
        Assert.Throws<IOException>(() =>
            AtomicFileWriter.Write(path, stream =>
                ProjectFileStore.Save(new ThrowingStream(stream, failAfterBytes: 300), document)));

        Assert.Equal(savedBytes, File.ReadAllBytes(path));
        ProjectAssert.DocumentsEqual(document, ProjectFileStore.Load(path).Document);
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void Save_InvalidMeshData_KeepsPreviousProjectIntact()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, document);
        byte[] savedBytes = File.ReadAllBytes(path);

        var broken = new ProjectDocument();
        broken.Scene.Root.AddChild(new MeshNode
        {
            Name = "Неконечная геометрия",
            Mesh = ProjectScenes.TriangleMesh(
                [new Vector3d(double.NaN, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0)],
                [0, 1, 2]),
        });

        var ex = Assert.Throws<ProjectFileException>(() => ProjectFileStore.Save(path, broken));
        Assert.Contains("неконечные", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(savedBytes, File.ReadAllBytes(path));
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void Save_DuplicateDefinitionIds_KeepsPreviousProjectIntact()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, document);
        byte[] savedBytes = File.ReadAllBytes(path);

        Guid sharedId = Guid.NewGuid();
        var broken = new ProjectDocument();
        var assembly = new GroupNode { Name = "Сборка" };
        for (int i = 0; i < 2; i++)
        {
            var definition = new ComponentDefinition { Id = sharedId, Name = $"Дубль {i}" };
            definition.Content = new MeshNode { Name = $"Меш {i}", Mesh = ProjectScenes.Cube() };
            assembly.AddChild(new ComponentInstanceNode { Name = $"Инстанс {i}", Definition = definition });
        }

        broken.Scene.Root.AddChild(assembly);

        var ex = Assert.Throws<ProjectFileException>(() => ProjectFileStore.Save(path, broken));
        Assert.Contains("одинаковый id", ex.Message, StringComparison.Ordinal);

        Assert.Equal(savedBytes, File.ReadAllBytes(path));
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void Save_FailureWithoutPreviousFile_CreatesNoPartialFile()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        Assert.Throws<IOException>(() =>
            AtomicFileWriter.Write(path, stream =>
                ProjectFileStore.Save(new ThrowingStream(stream, failAfterBytes: 100), ProjectScenes.ComplexDocument())));

        Assert.False(File.Exists(path));
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void Save_Overwrite_ReplacesContentCompletely()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);

        ProjectDocument first = ProjectScenes.ComplexDocument();
        ProjectFileStore.Save(path, first);
        byte[] firstBytes = File.ReadAllBytes(path);

        var second = new ProjectDocument { Unit = LengthUnit.Inch };
        ProjectFileStore.Save(path, second);
        byte[] secondBytes = File.ReadAllBytes(path);

        Assert.NotEqual(firstBytes, secondBytes);
        Assert.Equal(LengthUnit.Inch, ProjectFileStore.Load(path).Document.Unit);
        Assert.Empty(LeftoverTempFiles(temp.Root));
    }

    [Fact]
    public void LeftoverTempFile_DoesNotAffectLoadAndDetect()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectDocument document = ProjectScenes.ComplexDocument();
        ProjectFileStore.Save(path, document);

        // «Хвост» процесса, убитого в момент записи.
        File.WriteAllBytes(temp.FilePath(".scene.sna.deadbeef.tmp"), [0, 1, 2]);

        Assert.True(ProjectFileStore.IsProjectFile(path));
        ProjectAssert.DocumentsEqual(document, ProjectFileStore.Load(path).Document);
        Assert.Single(LeftoverTempFiles(temp.Root));
    }

    private static string[] LeftoverTempFiles(string directory) =>
        Directory.GetFiles(directory, "*.tmp");
}
