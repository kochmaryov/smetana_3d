using System.Buffers.Binary;

namespace SmetaNA.Licensing;

/// <summary>
/// Полезная нагрузка лицензионного ключа: идентификатор, тип, даты, лимит активаций в год и хеш лицензиата.
/// Сериализуется в 37 байт с фиксированным порядком полей (см. <see cref="ToPayloadBytes"/>).
/// </summary>
public sealed class LicenseKeyData
{
    /// <summary>Размер каноничной полезной нагрузки версии 1.</summary>
    internal const int PayloadSize = 37;

    private const byte FormatVersion = 1;
    private const byte ProductId = 1;
    private const byte NoFlags = 0;
    private const int LicenseeHashSize = 8;
    private const long MinUnixSeconds = 946684800; // 2000-01-01T00:00:00Z
    private const long MaxUnixSeconds = 4294967295; // 2106-02-07T06:28:15Z — предел uint32

    private readonly byte[] _licenseeHash;

    /// <summary>Создаёт данные ключа; хеш лицензиата вычисляется из строки <paramref name="licensee"/>.</summary>
    public LicenseKeyData(
        Guid keyId,
        LicenseKind kind,
        DateTimeOffset issuedAt,
        DateTimeOffset? expiresAt,
        int maxActivationsPerYear,
        string? licensee = null)
        : this(keyId, kind, issuedAt, expiresAt, maxActivationsPerYear, ComputeLicenseeHash(licensee))
    {
    }

    internal LicenseKeyData(
        Guid keyId,
        LicenseKind kind,
        DateTimeOffset issuedAt,
        DateTimeOffset? expiresAt,
        int maxActivationsPerYear,
        byte[] licenseeHash)
    {
        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ключа не может быть пустым.", nameof(keyId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Неизвестный тип лицензии.");
        }

        if (maxActivationsPerYear is < 1 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxActivationsPerYear),
                $"Лимит активаций в год должен быть в диапазоне 1..{byte.MaxValue}.");
        }

        if (expiresAt is not null && expiresAt <= issuedAt)
        {
            throw new ArgumentException("Дата окончания должна быть позже даты выпуска.", nameof(expiresAt));
        }

        ArgumentNullException.ThrowIfNull(licenseeHash);
        if (licenseeHash.Length != LicenseeHashSize)
        {
            throw new ArgumentException($"Хеш лицензиата должен быть {LicenseeHashSize} байт.", nameof(licenseeHash));
        }

        EnsureUnixRange(issuedAt, nameof(issuedAt));
        if (expiresAt is { } expiry)
        {
            EnsureUnixRange(expiry, nameof(expiresAt));
        }

        KeyId = keyId;
        Kind = kind;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        MaxActivationsPerYear = maxActivationsPerYear;
        _licenseeHash = [.. licenseeHash];
    }

    /// <summary>Идентификатор ключа (GUID) — им оперируют журнал активаций и список отзывов.</summary>
    public Guid KeyId { get; }

    /// <summary>Тип лицензии.</summary>
    public LicenseKind Kind { get; }

    /// <summary>Дата и время выпуска ключа (UTC).</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Дата и время окончания действия; <c>null</c> — бессрочно.</summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Максимум активаций устройства в скользящем окне 365 дней.</summary>
    public int MaxActivationsPerYear { get; }

    /// <summary>Хеш лицензиата (8 байт); сама строка лицензиата в ключ не попадает.</summary>
    public byte[] LicenseeHash => [.. _licenseeHash];

    /// <summary>Хеш строки лицензиата: первые 8 байт SHA-256 от нормализованного значения.</summary>
    public static byte[] ComputeLicenseeHash(string? licensee)
    {
        if (string.IsNullOrWhiteSpace(licensee))
        {
            return new byte[LicenseeHashSize];
        }

        string normalized = licensee.Trim().ToLowerInvariant();
        byte[] digest = LicenseCrypto.Sha256("smetana-licensee/v1|" + normalized);
        return digest[..LicenseeHashSize];
    }

    /// <summary>Совпадает ли хеш ключа с хешем переданного лицензиата.</summary>
    public bool MatchesLicensee(string licensee)
    {
        ArgumentNullException.ThrowIfNull(licensee);
        return _licenseeHash.AsSpan().SequenceEqual(ComputeLicenseeHash(licensee));
    }

    /// <summary>Каноничная бинарная полезная нагрузка (37 байт).</summary>
    internal byte[] ToPayloadBytes()
    {
        var payload = new byte[PayloadSize];
        payload[0] = FormatVersion;
        Convert.FromHexString(KeyId.ToString("N")).CopyTo(payload, 1);
        payload[17] = ProductId;
        payload[18] = (byte)Kind;
        payload[19] = NoFlags;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20, 4), ToUnixSeconds(IssuedAt));
        BinaryPrimitives.WriteUInt32BigEndian(
            payload.AsSpan(24, 4),
            ExpiresAt is { } expiry ? ToUnixSeconds(expiry) : 0u);
        payload[28] = (byte)MaxActivationsPerYear;
        _licenseeHash.CopyTo(payload, 29);
        return payload;
    }

    /// <summary>Восстанавливает данные из каноничной полезной нагрузки.</summary>
    internal static LicenseKeyData FromPayloadBytes(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length != PayloadSize)
        {
            throw new LicenseFormatException($"Некорректная длина полезной нагрузки ключа: {payload.Length}.");
        }

        if (payload[0] != FormatVersion)
        {
            throw new LicenseFormatException($"Неподдерживаемая версия формата ключа: {payload[0]}.");
        }

        if (payload[17] != ProductId)
        {
            throw new LicenseFormatException($"Ключ выдан для другого продукта (код {payload[17]}).");
        }

        var keyId = Guid.Parse(Convert.ToHexString(payload.AsSpan(1, 16)).ToLowerInvariant());
        var kind = (LicenseKind)payload[18];
        if (!Enum.IsDefined(kind))
        {
            throw new LicenseFormatException($"Неизвестный тип лицензии: {payload[18]}.");
        }

        DateTimeOffset issuedAt = FromUnixSeconds(BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(20, 4)));
        uint expirySeconds = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(24, 4));
        DateTimeOffset? expiresAt = expirySeconds == 0 ? null : FromUnixSeconds(expirySeconds);
        int maxActivationsPerYear = payload[28];
        if (maxActivationsPerYear == 0)
        {
            throw new LicenseFormatException("Лимит активаций в год не может быть нулевым.");
        }

        byte[] licenseeHash = payload[29..37];
        try
        {
            return new LicenseKeyData(keyId, kind, issuedAt, expiresAt, maxActivationsPerYear, licenseeHash);
        }
        catch (ArgumentException ex)
        {
            throw new LicenseFormatException("Полезная нагрузка ключа повреждена: некорректные поля.", ex);
        }
    }

    private static void EnsureUnixRange(DateTimeOffset value, string paramName)
    {
        long seconds = value.ToUnixTimeSeconds();
        if (seconds < MinUnixSeconds || seconds > MaxUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "Дата должна попадать в диапазон 2000..2106 годов (ограничение формата).");
        }
    }

    private static uint ToUnixSeconds(DateTimeOffset value) => (uint)value.ToUnixTimeSeconds();

    private static DateTimeOffset FromUnixSeconds(uint seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds);
}
