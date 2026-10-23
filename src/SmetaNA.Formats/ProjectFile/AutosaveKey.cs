using System.Security.Cryptography;
using System.Text;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Ключи файлов автосохранения: по ключу живут маркер сессии
/// <c>&lt;key&gt;.session.json</c> и снимок <c>&lt;key&gt;.autosave.sna</c>
/// в каталоге <see cref="AutosaveOptions.RootDirectory"/>.
/// </summary>
public static class AutosaveKey
{
    internal const string SessionMarkerSuffix = ".session.json";
    internal const string SnapshotSuffix = ".autosave.sna";

    /// <summary>
    /// Ключ для проекта, сохранённого в файл: стабилен для одного пути
    /// (нормализованный полный путь, SHA-256 в имени — без коллизий и спецсимволов).
    /// </summary>
    public static string FromProjectPath(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        string normalized = Path.GetFullPath(projectPath).ToLowerInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "proj-" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>Ключ для несохранённого (untitled) документа.</summary>
    public static string FromUntitled(Guid documentId) => $"untitled-{documentId:N}";

    internal static string SessionMarkerFileName(string key) => key + SessionMarkerSuffix;

    internal static string SnapshotFileName(string key) => key + SnapshotSuffix;

    internal static void Validate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > 64)
        {
            throw new ArgumentException("Ключ автосохранения слишком длинный (максимум 64 символа).", nameof(key));
        }

        foreach (char c in key)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                throw new ArgumentException(
                    $"Ключ автосохранения «{key}» содержит недопустимый символ «{c}».", nameof(key));
            }
        }
    }
}
