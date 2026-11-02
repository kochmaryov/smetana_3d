using System.Text.Json;
using System.Text.Json.Serialization;
using SmetaNA.EditorUI.ToolLayout;

namespace SmetaNA.EditorUI.FloatingGroups;

/// <summary>Хранилище размещения перемещаемых групп (load/save/reset).</summary>
public interface IFloatingGroupsStore
{
    FloatingGroupsModel Load();

    void Save(FloatingGroupsModel model);
}

/// <summary>
/// JSON-хранилище размещений <c>floating-groups.json</c> в каталоге настроек
/// приложения (рядом с <c>tool-layout.json</c>): версия схемы, миграция через
/// <see cref="FloatingGroupsMigrations"/>, атомарная запись. Повреждённый или
/// более новой схемы файл не роняет приложение — размещения по умолчанию.
/// </summary>
public sealed class JsonFloatingGroupsStore : IFloatingGroupsStore
{
    /// <summary>Имя файла размещений.</summary>
    public const string FileName = "floating-groups.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonFloatingGroupsStore()
        : this(DefaultFilePath)
    {
    }

    public JsonFloatingGroupsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
    }

    /// <summary>Путь к файлу размещений по умолчанию.</summary>
    public static string DefaultFilePath =>
        Path.Combine(JsonToolLayoutStore.SettingsDirectory, FileName);

    /// <summary>Файл этого хранилища.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public FloatingGroupsModel Load()
    {
        FloatingGroupsFile? file = ReadFile();
        if (file?.Placements is null)
        {
            return FloatingGroupsModel.CreateDefault();
        }

        if (file.Version > FloatingGroupsModel.CurrentVersion)
        {
            return FloatingGroupsModel.CreateDefault();
        }

        return FloatingGroupsMigrations.UpgradeToCurrent(
            new FloatingGroupsModel(
                file.Version,
                [.. file.Placements.Select(placement => placement.ToModel())]),
            ToolPalette.ToolGroupRegistry.DefaultGroups.Select(group => group.Id).ToList());
    }

    /// <inheritdoc />
    public void Save(FloatingGroupsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        FloatingGroupsFile file = new()
        {
            Version = model.Version,
            Placements = [.. model.Groups.Select(FloatingGroupPlacementFile.FromModel)],
        };

        string json = JsonSerializer.Serialize(file, SerializerOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string tempPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private FloatingGroupsFile? ReadFile()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<FloatingGroupsFile>(
                File.ReadAllText(FilePath),
                SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private sealed class FloatingGroupsFile
    {
        public int Version { get; set; }

        public List<FloatingGroupPlacementFile>? Placements { get; set; }
    }

    private sealed class FloatingGroupPlacementFile
    {
        public string GroupId { get; set; } = string.Empty;

        public FloatingGroupState State { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public FloatingGroupPlacement ToModel() => new(GroupId, State, X, Y);

        public static FloatingGroupPlacementFile FromModel(FloatingGroupPlacement placement) => new()
        {
            GroupId = placement.GroupId,
            State = placement.State,
            X = placement.X,
            Y = placement.Y,
        };
    }
}
