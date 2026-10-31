using System.Text.Json;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Список недавних файлов проекта для стартового экрана: JSON в каталоге
/// состояния приложения (офлайн, без ретраев — сбой чтения не критичен).
/// </summary>
public static class RecentFilesStore
{
    /// <summary>Максимум хранимых путей.</summary>
    public const int MaxEntries = 10;

    private static readonly object Sync = new();

    private static string _filePath = DefaultFilePath;

    /// <summary>Путь по умолчанию: JSON в каталоге состояния приложения.</summary>
    public static string DefaultFilePath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "SmetaNA", "recent.json");
        }
    }

    /// <summary>
    /// Путь к файлу списка недавних проектов; переопределяется тестами,
    /// чтобы не зависеть от глобального состояния машины.
    /// </summary>
    public static string FilePath
    {
        get => _filePath;
        set => _filePath = string.IsNullOrWhiteSpace(value) ? DefaultFilePath : value;
    }

    /// <summary>Прочитать список путей (пустой список при любой ошибке).</summary>
    public static IReadOnlyList<string> Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return [];
                }

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Добавить путь в начало списка (дедупликация, срезка до <see cref="MaxEntries"/>)
    /// и сохранить; возвращает обновлённый список.
    /// </summary>
    public static IReadOnlyList<string> Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (Sync)
        {
            var entries = new List<string> { path };
            foreach (string existing in Load())
            {
                if (!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(existing);
                }

                if (entries.Count >= MaxEntries)
                {
                    break;
                }
            }

            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
            return entries;
        }
    }
}
