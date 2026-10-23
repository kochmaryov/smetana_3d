namespace SmetaNA.Formats.ProjectFile;

/// <summary>Настройки автосохранения (модуль M9).</summary>
public sealed class AutosaveOptions
{
    /// <summary>
    /// Каталог для маркеров сессий и снимков автосохранения.
    /// Для проектов без пути — каталог данных приложения; тесты и portable-режим
    /// передают свой каталог.
    /// </summary>
    public required string RootDirectory { get; init; }

    /// <summary>Период периодического автосохранения (по умолчанию — раз в 2 минуты).</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Удалять снимок при штатном закрытии сессии (после успешного сохранения проекта).</summary>
    public bool DeleteSnapshotOnClose { get; init; } = true;
}

/// <summary>Сведения об открытой сессии редактирования (маркер незакрытой сессии).</summary>
/// <param name="Key">Ключ автосохранения.</param>
/// <param name="ProjectPath">Путь к файлу проекта, если сессия открыта по файлу.</param>
/// <param name="SessionId">Идентификатор сессии.</param>
/// <param name="OpenedAtUtc">Момент открытия сессии (UTC).</param>
/// <param name="LastAutosaveAtUtc">Момент последнего снимка (UTC), если был.</param>
public sealed record AutosaveSessionInfo(
    string Key,
    string? ProjectPath,
    Guid SessionId,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? LastAutosaveAtUtc);

/// <summary>Информация о выполненном снимке автосохранения.</summary>
/// <param name="Path">Путь к файлу снимка.</param>
/// <param name="SavedAtUtc">Момент записи снимка (UTC).</param>
/// <param name="ByteLength">Размер снимка в байтах.</param>
public sealed record AutosaveSnapshot(string Path, DateTimeOffset SavedAtUtc, long ByteLength);
