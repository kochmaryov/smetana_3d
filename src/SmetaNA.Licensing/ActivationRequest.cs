namespace SmetaNA.Licensing;

/// <summary>
/// Запрос активации: клиент передаёт продавцу (по почте, мессенджеру или на флешке) вместе с копией ключа.
/// Формат: <c>SMNQ1.полезная-нагрузка</c>, Crockford Base32 с группировкой по 5 символов.
/// Подписью не сопровождается: это заявка, а не полномочие; выдаётся только под конкретный ключ.
/// </summary>
public sealed class ActivationRequest
{
    /// <summary>Префикс формата запроса версии 1.</summary>
    public const string RequestPrefix = "SMNQ1";

    private const byte FormatVersion = 1;

    /// <summary>Размер каноничной полезной нагрузки версии 1.</summary>
    internal const int PayloadSize = 1 + PayloadCodec.GuidSize + PayloadCodec.GuidSize + PayloadCodec.HashSize + PayloadCodec.TimeSize;

    internal ActivationRequest(Guid requestId, Guid keyId, string deviceFingerprintHash, DateTimeOffset requestedAt)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор запроса не может быть пустым.", nameof(requestId));
        }

        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ключа не может быть пустым.", nameof(keyId));
        }

        RequestId = requestId;
        KeyId = keyId;
        DeviceFingerprintHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash);
        RequestedAt = requestedAt;
    }

    /// <summary>Идентификатор запроса — для сопоставления с выданным токеном.</summary>
    public Guid RequestId { get; }

    /// <summary>Ключ, к которому относится запрос.</summary>
    public Guid KeyId { get; }

    /// <summary>Связывающий хеш устройства (node-lock).</summary>
    public string DeviceFingerprintHash { get; }

    /// <summary>Момент формирования запроса (UTC).</summary>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Создаёт новый запрос активации.</summary>
    public static ActivationRequest Create(Guid keyId, string deviceFingerprintHash, DateTimeOffset requestedAt) =>
        new(Guid.NewGuid(), keyId, deviceFingerprintHash, requestedAt);

    /// <summary>Каноничная строка запроса.</summary>
    public string ToRequestString() => LicenseText.Format(RequestPrefix, ToPayloadBytes());

    /// <inheritdoc />
    public override string ToString() => ToRequestString();

    /// <summary>Разбирает строку запроса.</summary>
    public static ActivationRequest Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] payload = LicenseText.Split(text, RequestPrefix, 1)[0];
        if (payload.Length != PayloadSize)
        {
            throw new LicenseFormatException($"Некорректная длина полезной нагрузки запроса: {payload.Length}.");
        }

        if (payload[0] != FormatVersion)
        {
            throw new LicenseFormatException($"Неподдерживаемая версия формата запроса: {payload[0]}.");
        }

        int offset = 1;
        Guid requestId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        Guid keyId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        string deviceHash = PayloadCodec.ReadHash(payload, offset);
        offset += PayloadCodec.HashSize;
        DateTimeOffset requestedAt = PayloadCodec.ReadTime(payload, offset);
        try
        {
            return new ActivationRequest(requestId, keyId, deviceHash, requestedAt);
        }
        catch (ArgumentException ex)
        {
            throw new LicenseFormatException("Полезная нагрузка запроса повреждена: некорректные поля.", ex);
        }
    }

    /// <summary>Пытается разобрать строку запроса.</summary>
    public static bool TryParse(string text, out ActivationRequest? request)
    {
        try
        {
            request = Parse(text);
            return true;
        }
        catch (LicenseFormatException)
        {
            request = null;
            return false;
        }
        catch (ArgumentException)
        {
            request = null;
            return false;
        }
    }

    internal byte[] ToPayloadBytes()
    {
        var payload = new byte[PayloadSize];
        payload[0] = FormatVersion;
        int offset = 1;
        PayloadCodec.WriteGuid(payload, offset, RequestId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteGuid(payload, offset, KeyId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteHash(payload, offset, DeviceFingerprintHash);
        offset += PayloadCodec.HashSize;
        PayloadCodec.WriteTime(payload, offset, RequestedAt);
        return payload;
    }
}
