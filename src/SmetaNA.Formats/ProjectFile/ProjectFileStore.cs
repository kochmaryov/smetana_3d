using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>Результат чтения файла проекта: документ и сведения о версии схемы.</summary>
/// <param name="Document">Прочитанный документ проекта.</param>
/// <param name="SourceSchemaVersion">Версия схемы в исходном файле.</param>
/// <param name="Migrated">Файл был мигрирован со старой версии схемы.</param>
public sealed record ProjectLoadResult(ProjectDocument Document, int SourceSchemaVersion, bool Migrated);

/// <summary>
/// Фасад файловой подсистемы проектов (модуль M9): сохранение/чтение/детект
/// формата SmetaNA (ZIP: JSON-манифест + бинарные ассеты мешей).
/// Запись в файл атомарна (временный файл + замена) — сбой не оставляет
/// полусохранённый проект и не портит прежний файл.
/// </summary>
public static class ProjectFileStore
{
    private static readonly DateTimeOffset ZipEntryEpoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Сохранить документ в файл проекта (атомарно).</summary>
    public static void Save(string path, ProjectDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        AtomicFileWriter.Write(path, stream => Save(stream, document));
    }

    /// <summary>Сохранить документ в поток (без атомарной замены — забота вызывающей стороны).</summary>
    public static void Save(Stream stream, ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);

        var writer = new ProjectGraphWriter();
        ProjectManifestDto manifest = writer.Capture(document);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(archive, ProjectFileFormat.ManifestEntryName, SerializeManifest(manifest));
        foreach (ProjectAssetRecord asset in writer.Assets)
        {
            WriteEntry(archive, asset.Path, asset.Bytes);
        }
    }

    /// <summary>Прочитать документ из файла проекта.</summary>
    public static ProjectLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Load(File.ReadAllBytes(path));
    }

    /// <summary>Прочитать документ из потока.</summary>
    public static ProjectLoadResult Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Load(buffer.ToArray());
    }

    /// <summary>Прочитать документ из байтов контейнера.</summary>
    public static ProjectLoadResult Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            return LoadCore(data);
        }
        catch (ProjectFileException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or EndOfStreamException
            or InvalidOperationException or FormatException or OverflowException)
        {
            throw new ProjectFileException("Файл проекта повреждён или имеет неизвестный формат.", ex);
        }
    }

    /// <summary>Определить, является ли файл проектом SmetaNA (без чтения сцены).</summary>
    public static bool IsProjectFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return IsProjectFile(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Определить, является ли содержимое потока проектом SmetaNA (позиция потока сохраняется).</summary>
    public static bool IsProjectFile(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        long position = stream.CanSeek ? stream.Position : 0;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        return IsProjectFile(buffer.ToArray());
    }

    /// <summary>Определить, являются ли байты проектом SmetaNA.</summary>
    public static bool IsProjectFile(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            ZipArchiveEntry? manifestEntry = archive.GetEntry(ProjectFileFormat.ManifestEntryName);
            if (manifestEntry is null)
            {
                return false;
            }

            using Stream manifestStream = manifestEntry.Open();
            var manifest = JsonNode.Parse(manifestStream) as JsonObject;
            return manifest?["format"]?.GetValue<string>() == ProjectFileFormat.FormatId;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static ProjectLoadResult LoadCore(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry manifestEntry = archive.GetEntry(ProjectFileFormat.ManifestEntryName)
            ?? throw new ProjectFileException(
                $"В контейнере нет манифеста {ProjectFileFormat.ManifestEntryName} — это не файл проекта SmetaNA.");

        JsonObject manifest = ParseManifest(manifestEntry);
        if (manifest["format"]?.GetValue<string>() != ProjectFileFormat.FormatId)
        {
            throw new ProjectFileException("Манифест имеет чужой идентификатор формата — это не файл проекта SmetaNA.");
        }

        int sourceVersion = manifest["schemaVersion"]?.GetValue<int>()
            ?? throw new ProjectFileException("В манифесте не задана версия схемы — файл повреждён.");

        if (sourceVersion > ProjectFileFormat.CurrentSchemaVersion)
        {
            throw new ProjectFileException(
                $"Файл создан более новой версией SmetaNA (схема {sourceVersion}, поддерживается до " +
                $"{ProjectFileFormat.CurrentSchemaVersion}) — обновите приложение.");
        }

        bool migrated = sourceVersion < ProjectFileFormat.CurrentSchemaVersion;
        if (migrated)
        {
            ProjectSchemaMigrator.Upgrade(manifest, sourceVersion);
        }

        ProjectManifestDto dto = manifest.Deserialize<ProjectManifestDto>(ProjectJson.Options)
            ?? throw new ProjectFileException("Манифест проекта пуст — файл повреждён.");

        Dictionary<string, HalfEdgeMesh> meshes = LoadAssets(archive, dto);
        ProjectDocument document = ProjectGraphReader.Build(dto, meshes);
        return new ProjectLoadResult(document, sourceVersion, migrated);
    }

    private static JsonObject ParseManifest(ZipArchiveEntry entry)
    {
        try
        {
            using Stream stream = entry.Open();
            return JsonNode.Parse(stream) as JsonObject
                ?? throw new ProjectFileException("Манифест проекта не является JSON-объектом — файл повреждён.");
        }
        catch (ProjectFileException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ProjectFileException("Манифест проекта не читается как JSON — файл повреждён.", ex);
        }
    }

    private static Dictionary<string, HalfEdgeMesh> LoadAssets(ZipArchive archive, ProjectManifestDto dto)
    {
        var meshes = new Dictionary<string, HalfEdgeMesh>();
        foreach (ProjectAssetDto asset in dto.Assets ?? [])
        {
            if (!string.Equals(asset.Kind, ProjectFileFormat.MeshAssetKind, StringComparison.Ordinal))
            {
                throw new ProjectFileException($"Неизвестный вид ассета «{asset.Kind}» — файл создан более новой версией.");
            }

            ZipArchiveEntry entry = archive.GetEntry(asset.Path)
                ?? throw new ProjectFileException($"Ассет «{asset.Path}» отсутствует в контейнере — файл повреждён.");

            using Stream stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();

            if (bytes.Length != asset.ByteLength)
            {
                throw new ProjectFileException($"Ассет «{asset.Path}» имеет неожиданный размер — файл повреждён.");
            }

            string hash = MeshAssetCodec.ComputeHash(bytes);
            if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new ProjectFileException($"Контрольная сумма ассета «{asset.Path}» не сошлась — файл повреждён.");
            }

            if (!meshes.TryAdd(asset.Id, MeshAssetCodec.Decode(bytes)))
            {
                throw new ProjectFileException($"Дублирующийся идентификатор ассета «{asset.Id}» — файл повреждён.");
            }
        }

        return meshes;
    }

    private static byte[] SerializeManifest(ProjectManifestDto manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(manifest, ProjectJson.Options);

    private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = ZipEntryEpoch;
        using Stream stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
