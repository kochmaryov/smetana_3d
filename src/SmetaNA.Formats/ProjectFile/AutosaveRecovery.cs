using System.Text.Json;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Кандидат на восстановление: незакрытое автосохранение (маркер сессии остался
/// после сбоя) с последним снимком проекта.
/// </summary>
/// <param name="Key">Ключ автосохранения.</param>
/// <param name="AutosavePath">Путь к снимку.</param>
/// <param name="MarkerPath">Путь к маркеру сессии.</param>
/// <param name="Session">Сведения о сессии (из маркера, если он читается).</param>
/// <param name="SavedAtUtc">Момент последнего снимка (UTC).</param>
/// <param name="IsValid">Снимок выглядит целым (контейнер проекта читается).</param>
/// <param name="Problem">Описание проблемы, если снимок не цел.</param>
public sealed record RecoveryCandidate(
    string Key,
    string AutosavePath,
    string MarkerPath,
    AutosaveSessionInfo Session,
    DateTimeOffset SavedAtUtc,
    bool IsValid,
    string? Problem)
{
    /// <summary>Путь к файлу проекта, к которому относится автосохранение (если известен).</summary>
    public string? ProjectPath => Session.ProjectPath;
}

/// <summary>
/// Обнаружение и восстановление незакрытых автосохранений (модуль M9).
/// Признак сбоя — оставшийся маркер сессии (<c>&lt;key&gt;.session.json</c>):
/// штатное закрытие удаляет маркер и снимок.
/// </summary>
public static class AutosaveRecovery
{
    /// <summary>Все незакрытые автосохранения в каталоге (новые — первыми).</summary>
    public static IReadOnlyList<RecoveryCandidate> FindAll(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        List<RecoveryCandidate> candidates = [];
        foreach (string markerPath in Directory.EnumerateFiles(rootDirectory, "*" + AutosaveKey.SessionMarkerSuffix))
        {
            string key = Path.GetFileName(markerPath)[..^AutosaveKey.SessionMarkerSuffix.Length];
            RecoveryCandidate? candidate = BuildCandidate(rootDirectory, key);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return [.. candidates.OrderByDescending(c => c.SavedAtUtc)];
    }

    /// <summary>Найти незакрытое автосохранение по ключу.</summary>
    public static RecoveryCandidate? Find(string rootDirectory, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        AutosaveKey.Validate(key);
        return BuildCandidate(rootDirectory, key);
    }

    /// <summary>Восстановить документ из снимка; повреждённый снимок — <see cref="ProjectFileException"/>.</summary>
    public static ProjectLoadResult Restore(RecoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!candidate.IsValid)
        {
            throw new ProjectFileException(
                $"Автосохранение «{candidate.AutosavePath}» повреждено: {candidate.Problem ?? "неполный контейнер"}.");
        }

        return ProjectFileStore.Load(candidate.AutosavePath);
    }

    /// <summary>Отказаться от восстановления: маркер и снимок удаляются.</summary>
    public static void Discard(RecoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        File.Delete(candidate.AutosavePath);
        File.Delete(candidate.MarkerPath);
    }

    private static RecoveryCandidate? BuildCandidate(string rootDirectory, string key)
    {
        string markerPath = Path.Combine(rootDirectory, AutosaveKey.SessionMarkerFileName(key));
        string snapshotPath = Path.Combine(rootDirectory, AutosaveKey.SnapshotFileName(key));
        if (!File.Exists(markerPath) || !File.Exists(snapshotPath))
        {
            return null;
        }

        DateTimeOffset snapshotTimeUtc = File.GetLastWriteTimeUtc(snapshotPath);
        AutosaveSessionInfo session = ReadMarker(markerPath, key, snapshotTimeUtc)
            ?? new AutosaveSessionInfo(
                key,
                ProjectPath: null,
                SessionId: Guid.Empty,
                OpenedAtUtc: File.GetLastWriteTimeUtc(markerPath),
                LastAutosaveAtUtc: snapshotTimeUtc);

        bool isValid = ProjectFileStore.IsProjectFile(snapshotPath);
        return new RecoveryCandidate(
            key,
            snapshotPath,
            markerPath,
            session,
            session.LastAutosaveAtUtc ?? snapshotTimeUtc,
            isValid,
            isValid ? null : "снимок повреждён или записан не полностью");
    }

    private static AutosaveSessionInfo? ReadMarker(string markerPath, string key, DateTimeOffset snapshotTimeUtc)
    {
        try
        {
            var marker = JsonSerializer.Deserialize<AutosaveSessionMarkerDto>(
                File.ReadAllBytes(markerPath), ProjectJson.Options);
            return marker is null
                ? null
                : new AutosaveSessionInfo(
                    marker.Key.Length > 0 ? marker.Key : key,
                    marker.ProjectPath,
                    marker.SessionId,
                    marker.OpenedAtUtc,
                    marker.LastAutosaveAtUtc ?? snapshotTimeUtc);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
