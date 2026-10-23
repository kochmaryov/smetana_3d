using System.Text.Json;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Автосохранение проекта (модуль M9): периодические снимки документа, атомарная
/// запись (временный файл + замена) и маркер сессии для обнаружения сбоя.
/// <para>
/// Протокол восстановления: при открытии проекта вызывается <see cref="OpenSession"/>,
/// пишется маркер <c>&lt;key&gt;.session.json</c>; снимки пишутся в
/// <c>&lt;key&gt;.autosave.sna</c> (обычный файл проекта — его можно открыть напрямую).
/// При штатном закрытии (<see cref="CloseSession"/>) маркер и снимок удаляются.
/// Если маркер остался — сессия не была закрыта (сбой), и <see cref="AutosaveRecovery"/>
/// предлагает восстановить последний снимок.
/// </para>
/// <para>
/// Потокобезопасность: <see cref="SaveSnapshot"/> можно вызывать из любого потока;
/// периодический таймер не запускает параллельных снимков. Передача документа
/// через <c>capture</c> — ответственность вызывающей стороны (снимок должен быть
/// согласованным, например, под блокировкой модели).
/// </para>
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly Func<ProjectDocument> _capture;
    private readonly AutosaveOptions _options;
    private readonly object _sync = new();
    private Timer? _timer;
    private int _saveInFlight;

    public AutosaveService(string key, Func<ProjectDocument> capture, AutosaveOptions options)
    {
        AutosaveKey.Validate(key);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(options);

        Key = key;
        _capture = capture;
        _options = options;
        Directory.CreateDirectory(options.RootDirectory);
        AutosavePath = Path.Combine(options.RootDirectory, AutosaveKey.SnapshotFileName(key));
        SessionMarkerPath = Path.Combine(options.RootDirectory, AutosaveKey.SessionMarkerFileName(key));
    }

    /// <summary>Ключ автосохранения (и сессии).</summary>
    public string Key { get; }

    /// <summary>Путь к файлу снимка автосохранения.</summary>
    public string AutosavePath { get; }

    /// <summary>Путь к маркеру сессии.</summary>
    public string SessionMarkerPath { get; }

    /// <summary>Текущая сессия (после <see cref="OpenSession"/> или первого снимка).</summary>
    public AutosaveSessionInfo? Session { get; private set; }

    /// <summary>Последняя ошибка периодического снимка (ручные снимки бросают исключение).</summary>
    public Exception? LastError { get; private set; }

    /// <summary>Открыть сессию редактирования: пишется маркер незакрытой сессии.</summary>
    public AutosaveSessionInfo OpenSession(string? projectPath = null)
    {
        lock (_sync)
        {
            var session = new AutosaveSessionInfo(Key, projectPath, Guid.NewGuid(), DateTimeOffset.UtcNow, null);
            WriteMarker(session);
            Session = session;
            return session;
        }
    }

    /// <summary>
    /// Выполнить снимок автосохранения атомарно: снимок заменяется целиком,
    /// затем обновляется маркер сессии. При сбое прежний снимок остаётся нетронутым.
    /// </summary>
    public AutosaveSnapshot SaveSnapshot()
    {
        lock (_sync)
        {
            ProjectDocument document = _capture();
            AtomicFileWriter.Write(AutosavePath, stream => ProjectFileStore.Save(stream, document));

            DateTimeOffset savedAtUtc = DateTimeOffset.UtcNow;
            Session = (Session ?? new AutosaveSessionInfo(Key, null, Guid.NewGuid(), savedAtUtc, null)) with
            {
                LastAutosaveAtUtc = savedAtUtc,
            };
            WriteMarker(Session);
            return new AutosaveSnapshot(AutosavePath, savedAtUtc, new FileInfo(AutosavePath).Length);
        }
    }

    /// <summary>Запустить периодическое автосохранение с периодом <see cref="AutosaveOptions.Interval"/>.</summary>
    public void Start()
    {
        if (_options.Interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Период автосохранения должен быть положительным.");
        }

        lock (_sync)
        {
            _timer ??= new Timer(_ => OnTimerTick(), null, _options.Interval, _options.Interval);
        }
    }

    /// <summary>Остановить периодическое автосохранение.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// Штатно закрыть сессию: маркер удаляется (сбой больше не детектируется),
    /// снимок удаляется, если задано <see cref="AutosaveOptions.DeleteSnapshotOnClose"/>.
    /// </summary>
    public void CloseSession()
    {
        lock (_sync)
        {
            if (_options.DeleteSnapshotOnClose)
            {
                TryDelete(AutosavePath);
            }

            TryDelete(SessionMarkerPath);
            Session = null;
        }
    }

    /// <summary>Остановить таймер. Сессия намеренно не закрывается — «сбой» может быть имитирован Dispose.</summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void OnTimerTick()
    {
        if (Interlocked.Exchange(ref _saveInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            SaveSnapshot();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            Interlocked.Exchange(ref _saveInFlight, 0);
        }
    }

    private void WriteMarker(AutosaveSessionInfo session)
    {
        var marker = new AutosaveSessionMarkerDto
        {
            Key = session.Key,
            ProjectPath = session.ProjectPath,
            SessionId = session.SessionId,
            OpenedAtUtc = session.OpenedAtUtc,
            LastAutosaveAtUtc = session.LastAutosaveAtUtc,
        };
        AtomicFileWriter.Write(SessionMarkerPath, stream =>
            JsonSerializer.Serialize(stream, marker, ProjectJson.Options));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>DTO маркера сессии в JSON (каталог автосохранения).</summary>
internal sealed class AutosaveSessionMarkerDto
{
    public required string Key { get; init; }

    public string? ProjectPath { get; init; }

    public required Guid SessionId { get; init; }

    public required DateTimeOffset OpenedAtUtc { get; init; }

    public DateTimeOffset? LastAutosaveAtUtc { get; init; }

    public string Generator { get; init; } = ProjectDocument.DefaultGenerator;
}
