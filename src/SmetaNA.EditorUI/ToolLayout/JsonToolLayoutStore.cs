using System.Text.Json;
using System.Text.Json.Serialization;
using SmetaNA.EditorUI.ToolPalette;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Хранилище раскладки палитры в JSON (реализация <see cref="IToolLayoutStore"/>):
/// файл в каталоге настроек приложения (персистентный, не в bin), версия схемы и
/// миграция через <see cref="ToolLayoutMigrations"/>. Повреждённый, чужой или
/// более новой схемы файл не роняет приложение — возвращается дефолтная раскладка
/// <see cref="ToolLayoutModel.CreateDefault()"/> (контракт волны 0).
/// </summary>
public sealed class JsonToolLayoutStore : IToolLayoutStore
{
    /// <summary>Имя файла раскладки.</summary>
    public const string FileName = "tool-layout.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Хранилище в каталоге настроек приложения.</summary>
    public JsonToolLayoutStore()
        : this(DefaultFilePath)
    {
    }

    /// <summary>Хранилище в указанном файле (тесты, перенос настроек).</summary>
    public JsonToolLayoutStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
    }

    /// <summary>
    /// Каталог настроек приложения: <c>%APPDATA%/SmetaNA/settings</c> — рядом с
    /// состоянием лицензии, создаётся при необходимости.
    /// </summary>
    public static string SettingsDirectory
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "SmetaNA", "settings");
        }
    }

    /// <summary>Путь к файлу раскладки по умолчанию.</summary>
    public static string DefaultFilePath => Path.Combine(SettingsDirectory, FileName);

    /// <summary>Файл этого хранилища.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    public ToolLayoutModel Load()
    {
        ToolLayoutFile? file = ReadFile();
        if (file is null || file.Groups is null)
        {
            return ToolLayoutModel.CreateDefault();
        }

        if (file.Version > ToolLayoutModel.CurrentVersion)
        {
            // Схема из более новой версии приложения — не ломаем её, работаем дефолтом.
            return ToolLayoutModel.CreateDefault();
        }

        ToolLayoutModel loaded = new(
            file.Version,
            [.. file.Groups.OfType<ToolLayoutGroupFile>().Select(group => group.ToModel())]);
        return ToolLayoutMigrations.UpgradeToCurrent(loaded, ToolGroupRegistry.DefaultGroups);
    }

    /// <inheritdoc />
    public void Save(ToolLayoutModel layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ToolLayoutFile file = FromModel(layout);
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

    /// <inheritdoc />
    public ToolLayoutModel Reset()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Файл занят другим процессом — пользовательская раскладка остаётся,
            // но сброс всё равно применяется (дефолт возвращается ниже).
        }

        return ToolLayoutModel.CreateDefault();
    }

    private ToolLayoutFile? ReadFile()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ToolLayoutFile>(File.ReadAllText(FilePath), SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // Повреждённый или чужой файл — откат к дефолтной раскладке.
            return null;
        }
    }

    private static ToolLayoutFile FromModel(ToolLayoutModel layout) =>
        new()
        {
            Version = ToolLayoutModel.CurrentVersion,
            Groups = [.. layout.Groups.Select(group => new ToolLayoutGroupFile
            {
                Id = group.Id,
                Name = group.CustomName,
                Visible = group.Visible,
                Items = [.. group.Items.Select((item, index) => new ToolLayoutItemFile
                {
                    CommandId = item.CommandId,
                    Visible = item.Visible,
                    Order = index,
                })],
            })],
        };

    /// <summary>Файл раскладки (schema v1: <c>version</c>, <c>groups[{id,name,visible,items}]</c>).</summary>
    private sealed class ToolLayoutFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("groups")]
        public List<ToolLayoutGroupFile?>? Groups { get; set; }
    }

    private sealed class ToolLayoutGroupFile
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Имя пользовательской группы; <c>null</c> — имя из реестра.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; } = true;

        [JsonPropertyName("items")]
        public List<ToolLayoutItemFile?>? Items { get; set; }

        public ToolLayoutGroup ToModel() =>
            new(
                Id ?? string.Empty,
                Name,
                Visible,
                [.. (Items ?? [])
                    .OfType<ToolLayoutItemFile>()
                    .OrderBy(item => item.Order)
                    .Select(item => item.ToModel())]);
    }

    private sealed class ToolLayoutItemFile
    {
        [JsonPropertyName("commandId")]
        public string? CommandId { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; } = true;

        [JsonPropertyName("order")]
        public int Order { get; set; }

        public ToolLayoutItem ToModel() =>
            new(CommandId ?? string.Empty, Visible, Order);
    }
}
