using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>Запись журнала активаций ключа.</summary>
public sealed record ActivationRecord
{
    /// <summary>Ключ, для которого выдана активация.</summary>
    public required Guid KeyId { get; init; }

    /// <summary>Идентификатор токена активации.</summary>
    public required Guid TokenId { get; init; }

    /// <summary>Связывающий хеш устройства (node-lock).</summary>
    public required string DeviceFingerprintHash { get; init; }

    /// <summary>Момент выдачи (UTC).</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>Активация отозвана (деактивация/перенос).</summary>
    public bool Revoked { get; init; }

    /// <summary>Причина отзыва.</summary>
    public string? RevocationReason { get; init; }

    /// <summary>Момент отзыва (UTC).</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}

/// <summary>
/// Журнал активаций (сторона продавца): считает активации в скользящем окне 365 дней,
/// помечает деактивации и хранит данные для списка отзывов. Хранение — в памяти или JSON-файле офлайн-инструмента.
/// </summary>
public sealed class ActivationLedger
{
    /// <summary>Окно учёта лимита активаций «в год».</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(365);

    private readonly List<ActivationRecord> _records = [];
    private readonly TimeProvider _time;
    private readonly TimeSpan _window;

    /// <summary>Создает журнал.</summary>
    public ActivationLedger(TimeProvider? time = null, TimeSpan? window = null)
    {
        _time = time ?? TimeProvider.System;
        _window = window ?? DefaultWindow;
        if (_window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Окно учёта активаций должно быть положительным.");
        }
    }

    /// <summary>Все записи журнала (копия).</summary>
    public IReadOnlyList<ActivationRecord> Records => [.. _records];

    /// <summary>Добавляет запись о выдаче активации.</summary>
    public void AddActivation(Guid keyId, Guid tokenId, string deviceFingerprintHash, DateTimeOffset issuedAt)
    {
        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ключа не может быть пустым.", nameof(keyId));
        }

        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор токена не может быть пустым.", nameof(tokenId));
        }

        _records.Add(new ActivationRecord
        {
            KeyId = keyId,
            TokenId = tokenId,
            DeviceFingerprintHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash),
            IssuedAt = issuedAt,
        });
    }

    /// <summary>Число активаций ключа в скользящем окне (по умолчанию — на текущий момент).</summary>
    public int CountActivations(Guid keyId, DateTimeOffset? at = null)
    {
        DateTimeOffset point = at ?? _time.GetUtcNow();
        DateTimeOffset windowStart = point - _window;
        return _records.Count(record =>
            record.KeyId == keyId && record.IssuedAt > windowStart && record.IssuedAt <= point);
    }

    /// <summary>Записи активаций конкретного ключа (копия).</summary>
    public IReadOnlyList<ActivationRecord> GetActivations(Guid keyId) =>
        [.. _records.Where(record => record.KeyId == keyId)];

    /// <summary>Помечает активацию отозванной (деактивация или перенос лицензии).</summary>
    public bool Revoke(Guid tokenId, string? reason = null)
    {
        int index = _records.FindIndex(record => record.TokenId == tokenId);
        if (index < 0)
        {
            return false;
        }

        ActivationRecord record = _records[index];
        if (record.Revoked)
        {
            return true;
        }

        _records[index] = record with
        {
            Revoked = true,
            RevocationReason = reason,
            RevokedAt = _time.GetUtcNow(),
        };

        return true;
    }

    /// <summary>Отозвана ли активация.</summary>
    public bool IsRevoked(Guid tokenId) =>
        _records.Any(record => record.TokenId == tokenId && record.Revoked);

    /// <summary>Идентификаторы отозванных токенов.</summary>
    public IReadOnlyList<Guid> GetRevokedTokenIds() =>
        [.. _records.Where(record => record.Revoked).Select(record => record.TokenId)];

    /// <summary>Сериализует журнал в JSON (офлайн-инструмент продавца).</summary>
    public string ToJson() => JsonSerializer.Serialize(_records, LicenseJson.Canonical);

    /// <summary>Восстанавливает журнал из JSON.</summary>
    public static ActivationLedger FromJson(string json, TimeProvider? time = null, TimeSpan? window = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<ActivationRecord>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<ActivationRecord>>(json, LicenseJson.Canonical);
        }
        catch (JsonException ex)
        {
            throw new LicenseFormatException("Журнал активаций повреждён: некорректный JSON.", ex);
        }

        var ledger = new ActivationLedger(time, window);
        ledger._records.AddRange(records ?? throw new LicenseFormatException("Журнал активаций повреждён: пустое содержимое."));
        return ledger;
    }
}
