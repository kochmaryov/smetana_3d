using System.Text.Json.Nodes;
using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Версионирование формата (задача 4.1): чтение старой схемы через миграцию-заготовку,
/// отказ от файлов более новой версии, терпимость к неизвестным полям (чтение файлов
/// будущих минорных версий).
/// </summary>
public sealed class ProjectMigrationTests
{
    [Fact]
    public void Load_Schema0File_MigratesToCurrentSchema()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        expected.Unit = LengthUnit.Millimeter;
        expected.View = ProjectViewSettings.Default with { Projection = ProjectProjectionMode.Orthographic };
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, expected);

        byte[] legacy = ProjectZip.RewriteManifest(File.ReadAllBytes(path), ToSchema0);

        ProjectLoadResult result = ProjectFileStore.Load(legacy);

        Assert.True(result.Migrated);
        Assert.Equal(0, result.SourceSchemaVersion);
        ProjectAssert.DocumentsEqual(expected, result.Document);
    }

    [Fact]
    public void Load_FutureSchemaVersion_ThrowsClearError()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, ProjectScenes.ComplexDocument());

        byte[] future = ProjectZip.RewriteManifest(
            File.ReadAllBytes(path),
            manifest =>
            {
                manifest["schemaVersion"] = 99;
                return manifest;
            });

        var ex = Assert.Throws<ProjectFileException>(() => ProjectFileStore.Load(future));
        Assert.Contains("более новой версией", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownFields_AreIgnored()
    {
        using var temp = new TempDirectory();
        ProjectDocument expected = ProjectScenes.ComplexDocument();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, expected);

        byte[] extended = ProjectZip.RewriteManifest(
            File.ReadAllBytes(path),
            manifest =>
            {
                manifest["futureTopLevel"] = new JsonObject { ["answer"] = 42 };
                ((JsonObject)manifest["scene"]!)["futureNodeField"] = "сохранится в будущих версиях";
                return manifest;
            });

        ProjectLoadResult result = ProjectFileStore.Load(extended);
        Assert.False(result.Migrated);
        ProjectAssert.DocumentsEqual(expected, result.Document);
    }

    [Fact]
    public void Load_ForeignManifestFormat_Throws()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, ProjectScenes.ComplexDocument());

        byte[] foreign = ProjectZip.RewriteManifest(
            File.ReadAllBytes(path),
            _ => new JsonObject { ["format"] = "Чужой.Формат", ["schemaVersion"] = 1 });

        Assert.Throws<ProjectFileException>(() => ProjectFileStore.Load(foreign));
        Assert.False(ProjectFileStore.IsProjectFile(foreign));
    }

    [Fact]
    public void Load_TruncatedContainer_Throws()
    {
        using var temp = new TempDirectory();
        string path = temp.FilePath("scene" + ProjectFileFormat.Extension);
        ProjectFileStore.Save(path, ProjectScenes.ComplexDocument());

        byte[] whole = File.ReadAllBytes(path);
        byte[] truncated = whole[..(whole.Length / 3)];
        Assert.Throws<ProjectFileException>(() => ProjectFileStore.Load(truncated));
    }

    /// <summary>
    /// Преобразовать манифест схемы 1 в черновую схему 0: единицы — коротким кодом
    /// «mm», вид — блоком «camera» с проекцией «ortho», типы узлов — полем «kind».
    /// </summary>
    private static JsonObject ToSchema0(JsonObject manifest)
    {
        manifest["schemaVersion"] = 0;

        string units = manifest["units"]!.GetValue<string>();
        manifest.Remove("units");
        manifest["unit"] = units switch
        {
            "millimeter" => "mm",
            "centimeter" => "cm",
            "meter" => "m",
            "inch" => "in",
            _ => throw new InvalidOperationException($"Неожиданные единицы «{units}»."),
        };

        var view = (JsonObject)manifest["view"]!;
        manifest.Remove("view");
        if (view["projection"]!.GetValue<string>() == "orthographic")
        {
            view["projection"] = "ortho";
        }

        manifest["camera"] = view;
        RenameTypes((JsonObject)manifest["scene"]!);
        if (manifest["components"] is JsonArray components)
        {
            foreach (JsonNode? component in components)
            {
                RenameTypes((JsonObject)component!["content"]!);
            }
        }

        return manifest;
    }

    private static void RenameTypes(JsonObject node)
    {
        if (node.Remove("type", out JsonNode? type) && type is not null)
        {
            node["kind"] = type;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (JsonNode? child in children)
            {
                RenameTypes((JsonObject)child!);
            }
        }
    }
}
