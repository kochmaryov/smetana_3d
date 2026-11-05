using System.Text.Json;
using SmetaNA.Core.Inference;
using SmetaNA.EditorUI.ToolLayout;

namespace SmetaNA.EditorUI.Settings;

/// <summary>
/// Настройки привязок в JSON рядом с раскладкой палитры (каталог настроек
/// приложения). Повреждённый или чужой файл не роняет приложение — берутся
/// значения по умолчанию (<see cref="InferenceSettings.Default"/>).
/// </summary>
public sealed class InferenceSettingsStore
{
    /// <summary>Имя файла настроек привязок.</summary>
    public const string FileName = "inference.json";

    /// <summary>Версия схемы файла.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public InferenceSettingsStore()
        : this(DefaultFilePath)
    {
    }

    public InferenceSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(JsonToolLayoutStore.SettingsDirectory, FileName);

    public string FilePath { get; }

    /// <summary>Прочитать настройки; отсутствие файла и ошибки чтения дают значения по умолчанию.</summary>
    public InferenceSettings Load()
    {
        InferenceSettingsFile? file;
        try
        {
            if (!File.Exists(FilePath))
            {
                return InferenceSettings.Default;
            }

            file = JsonSerializer.Deserialize<InferenceSettingsFile>(File.ReadAllText(FilePath), SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return InferenceSettings.Default;
        }

        if (file is null || file.Version > CurrentVersion)
        {
            return InferenceSettings.Default;
        }

        return new InferenceSettings
        {
            TolerancePx = file.TolerancePx,
            ShowLabels = file.ShowLabels,
            ShowHiddenGeometry = file.ShowHiddenGeometry,
            GridEnabled = file.GridEnabled,
            GridStep = file.GridStep,
            LengthSnappingEnabled = file.LengthSnappingEnabled,
            LengthPrecision = file.LengthPrecision,
            Categories = (SnapFilter)file.Categories,
        }.Normalize();
    }

    /// <summary>Записать настройки; ошибки записи не пробрасываются в UI.</summary>
    public void Save(InferenceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InferenceSettings normalized = settings.Normalize();

        var file = new InferenceSettingsFile
        {
            Version = CurrentVersion,
            TolerancePx = normalized.TolerancePx,
            ShowLabels = normalized.ShowLabels,
            ShowHiddenGeometry = normalized.ShowHiddenGeometry,
            GridEnabled = normalized.GridEnabled,
            GridStep = normalized.GridStep,
            LengthSnappingEnabled = normalized.LengthSnappingEnabled,
            LengthPrecision = normalized.LengthPrecision,
            Categories = (int)normalized.Categories,
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(file, SerializerOptions));
            File.Move(tempPath, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Настройки — не критичные данные: сбой записи не должен ломать работу.
        }
    }

    /// <summary>Схема файла (camelCase в JSON).</summary>
    private sealed class InferenceSettingsFile
    {
        public int Version { get; set; } = CurrentVersion;

        public double TolerancePx { get; set; } = SnapRequest.DefaultTolerancePx;

        public bool ShowLabels { get; set; } = true;

        public bool ShowHiddenGeometry { get; set; }

        public bool GridEnabled { get; set; }

        public double GridStep { get; set; } = InferenceSettings.DefaultGridStep;

        public bool LengthSnappingEnabled { get; set; }

        public double LengthPrecision { get; set; } = 0.1;

        public int Categories { get; set; } = (int)SnapFilter.All;
    }
}
