namespace SmetaNA.Licensing;

/// <summary>
/// Квитанция деактивации: клиент формирует её при отказе от активации на устройстве и передаёт продавцу
/// (по почте/мессенджеру) для отзыва токена. Формат: <c>SMND1.полезная-нагрузка</c>, Crockford Base32.
/// Квитанция не подписывается: это заявка на отзыв, а полномочие — ручная обработка продавцом.
/// </summary>
public sealed class DeactivationReceipt
{
    /// <summary>Префикс формата квитанции версии 1.</summary>
    public const string ReceiptPrefix = "SMND1";

    private const byte FormatVersion = 1;

    /// <summary>Размер каноничной полезной нагрузки версии 1.</summary>
    internal const int PayloadSize = 1 + PayloadCodec.GuidSize + PayloadCodec.GuidSize + PayloadCodec.HashSize + PayloadCodec.TimeSize;

    internal DeactivationReceipt(Guid keyId, Guid tokenId, string deviceFingerprintHash, DateTimeOffset deactivatedAt)
    {
        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ключа не может быть пустым.", nameof(keyId));
        }

        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор токена не может быть пустым.", nameof(tokenId));
        }

        KeyId = keyId;
        TokenId = tokenId;
        DeviceFingerprintHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash);
        DeactivatedAt = deactivatedAt;
    }

    /// <summary>Ключ, к которому относится активация.</summary>
    public Guid KeyId { get; }

    /// <summary>Отзываемый токен активации.</summary>
    public Guid TokenId { get; }

    /// <summary>Связывающий хеш устройства (node-lock).</summary>
    public string DeviceFingerprintHash { get; }

    /// <summary>Момент деактивации (UTC).</summary>
    public DateTimeOffset DeactivatedAt { get; }

    /// <summary>Каноничная строка квитанции.</summary>
    public string ToReceiptString() => LicenseText.Format(ReceiptPrefix, ToPayloadBytes());

    /// <inheritdoc />
    public override string ToString() => ToReceiptString();

    /// <summary>Разбирает строку квитанции.</summary>
    public static DeactivationReceipt Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] payload = LicenseText.Split(text, ReceiptPrefix, 1)[0];
        if (payload.Length != PayloadSize)
        {
            throw new LicenseFormatException($"Некорректная длина полезной нагрузки квитанции: {payload.Length}.");
        }

        if (payload[0] != FormatVersion)
        {
            throw new LicenseFormatException($"Неподдерживаемая версия формата квитанции: {payload[0]}.");
        }

        int offset = 1;
        Guid keyId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        Guid tokenId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        string deviceHash = PayloadCodec.ReadHash(payload, offset);
        offset += PayloadCodec.HashSize;
        DateTimeOffset deactivatedAt = PayloadCodec.ReadTime(payload, offset);
        try
        {
            return new DeactivationReceipt(keyId, tokenId, deviceHash, deactivatedAt);
        }
        catch (ArgumentException ex)
        {
            throw new LicenseFormatException("Полезная нагрузка квитанции повреждена: некорректные поля.", ex);
        }
    }

    internal byte[] ToPayloadBytes()
    {
        var payload = new byte[PayloadSize];
        payload[0] = FormatVersion;
        int offset = 1;
        PayloadCodec.WriteGuid(payload, offset, KeyId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteGuid(payload, offset, TokenId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteHash(payload, offset, DeviceFingerprintHash);
        offset += PayloadCodec.HashSize;
        PayloadCodec.WriteTime(payload, offset, DeactivatedAt);
        return payload;
    }
}
