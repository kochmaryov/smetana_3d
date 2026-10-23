using System.Diagnostics.CodeAnalysis;

namespace SmetaNA.Licensing;

/// <summary>
/// Токен активации — подписанное продавцом разрешение на работу конкретного ключа на конкретном устройстве (node-lock).
/// Формат: <c>SMNA1.полезная-нагрузка.подпись</c>, сегменты — Crockford Base32 с группировкой по 5 символов.
/// Токен выпускается продавцом в обмен на <see cref="ActivationRequest"/>; работает без сети до <see cref="ValidUntil"/>.
/// </summary>
public sealed class ActivationToken
{
    /// <summary>Префикс формата токена версии 1.</summary>
    public const string TokenPrefix = "SMNA1";

    /// <summary>Домен подписи — защищает от подмены формата данных.</summary>
    internal const string SignatureDomain = "SmetaNA.Licensing.ActivationToken/v1";

    /// <summary>Размер каноничной полезной нагрузки версии 1: версия + два GUID + две метки времени + номер + хеш.</summary>
    internal const int PayloadSize = 1 + (2 * PayloadCodec.GuidSize) + (2 * PayloadCodec.TimeSize) + 2 + PayloadCodec.HashSize;

    private const byte FormatVersion = 1;

    private readonly byte[] _signature;

    internal ActivationToken(
        Guid tokenId,
        Guid keyId,
        DateTimeOffset issuedAt,
        DateTimeOffset? validUntil,
        int activationOrdinal,
        string deviceFingerprintHash,
        byte[] signature)
    {
        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор токена не может быть пустым.", nameof(tokenId));
        }

        if (keyId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор ключа не может быть пустым.", nameof(keyId));
        }

        if (activationOrdinal is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(activationOrdinal), "Порядковый номер активации должен быть 1..65535.");
        }

        if (validUntil is not null && validUntil <= issuedAt)
        {
            throw new ArgumentException("Окончание действия токена должно быть позже его выдачи.", nameof(validUntil));
        }

        ArgumentNullException.ThrowIfNull(signature);
        if (signature.Length != LicenseCrypto.SignatureSize)
        {
            throw new ArgumentException($"Подпись должна быть {LicenseCrypto.SignatureSize} байт.", nameof(signature));
        }

        TokenId = tokenId;
        KeyId = keyId;
        IssuedAt = issuedAt;
        ValidUntil = validUntil;
        ActivationOrdinal = activationOrdinal;
        DeviceFingerprintHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash);
        _signature = [.. signature];
    }

    /// <summary>Идентификатор токена — им оперируют отзывы и переносы лицензии.</summary>
    public Guid TokenId { get; }

    /// <summary>Ключ, для которого выдан токен.</summary>
    public Guid KeyId { get; }

    /// <summary>Момент выдачи (UTC).</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>Окончание действия токена; <c>null</c> — до окончания действия ключа или отзыва.</summary>
    public DateTimeOffset? ValidUntil { get; }

    /// <summary>Порядковый номер активации ключа в году (скользящее окно 365 дней).</summary>
    public int ActivationOrdinal { get; }

    /// <summary>Связывающий хеш устройства (node-lock).</summary>
    public string DeviceFingerprintHash { get; }

    /// <summary>Подпись ECDSA P-256 (64 байта, r||s).</summary>
    public byte[] Signature => [.. _signature];

    /// <summary>Подпись без копирования — для внутренней проверки.</summary>
    internal byte[] SignatureCore => _signature;

    /// <summary>Собирает подписанный токен (сторона продавца).</summary>
    internal static ActivationToken Create(
        Guid keyId,
        string deviceFingerprintHash,
        DateTimeOffset issuedAt,
        DateTimeOffset? validUntil,
        int activationOrdinal,
        System.Security.Cryptography.ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);

        Guid tokenId = Guid.NewGuid();
        string deviceHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash);
        byte[] payload = BuildPayload(tokenId, keyId, issuedAt, validUntil, activationOrdinal, deviceHash);
        byte[] signature = LicenseCrypto.Sign(signingKey, SignatureDomain, payload);
        return new ActivationToken(tokenId, keyId, issuedAt, validUntil, activationOrdinal, deviceHash, signature);
    }

    /// <summary>Каноничная строка токена.</summary>
    public string ToTokenString() => LicenseText.Format(TokenPrefix, ToPayloadBytes(), _signature);

    /// <inheritdoc />
    public override string ToString() => ToTokenString();

    /// <summary>Разбирает строку токена; подпись не проверяется.</summary>
    public static ActivationToken Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[][] segments = LicenseText.Split(text, TokenPrefix, 2);
        if (segments[0].Length != PayloadSize)
        {
            throw new LicenseFormatException($"Некорректная длина полезной нагрузки токена: {segments[0].Length}.");
        }

        if (segments[1].Length != LicenseCrypto.SignatureSize)
        {
            throw new LicenseFormatException($"Некорректная длина подписи токена: {segments[1].Length}.");
        }

        byte[] payload = segments[0];
        if (payload[0] != FormatVersion)
        {
            throw new LicenseFormatException($"Неподдерживаемая версия формата токена: {payload[0]}.");
        }

        int offset = 1;
        Guid tokenId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        Guid keyId = PayloadCodec.ReadGuid(payload, offset);
        offset += PayloadCodec.GuidSize;
        DateTimeOffset issuedAt = PayloadCodec.ReadTime(payload, offset);
        offset += PayloadCodec.TimeSize;
        DateTimeOffset? validUntil = PayloadCodec.ReadOptionalTime(payload, offset);
        offset += PayloadCodec.TimeSize;
        int ordinal = (payload[offset] << 8) | payload[offset + 1];
        offset += 2;
        string deviceHash = PayloadCodec.ReadHash(payload, offset);

        try
        {
            return new ActivationToken(tokenId, keyId, issuedAt, validUntil, ordinal, deviceHash, segments[1]);
        }
        catch (ArgumentException ex)
        {
            throw new LicenseFormatException("Полезная нагрузка токена повреждена: некорректные поля.", ex);
        }
    }

    /// <summary>Пытается разобрать строку токена; подпись не проверяется.</summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out ActivationToken? token)
    {
        try
        {
            token = Parse(text);
            return true;
        }
        catch (LicenseFormatException)
        {
            token = null;
            return false;
        }
        catch (ArgumentException)
        {
            token = null;
            return false;
        }
    }

    internal byte[] ToPayloadBytes() =>
        BuildPayload(TokenId, KeyId, IssuedAt, ValidUntil, ActivationOrdinal, DeviceFingerprintHash);

    private static byte[] BuildPayload(
        Guid tokenId,
        Guid keyId,
        DateTimeOffset issuedAt,
        DateTimeOffset? validUntil,
        int activationOrdinal,
        string deviceFingerprintHash)
    {
        var payload = new byte[PayloadSize];
        payload[0] = FormatVersion;
        int offset = 1;
        PayloadCodec.WriteGuid(payload, offset, tokenId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteGuid(payload, offset, keyId);
        offset += PayloadCodec.GuidSize;
        PayloadCodec.WriteTime(payload, offset, issuedAt);
        offset += PayloadCodec.TimeSize;
        PayloadCodec.WriteOptionalTime(payload, offset, validUntil);
        offset += PayloadCodec.TimeSize;
        payload[offset] = (byte)(activationOrdinal >> 8);
        payload[offset + 1] = (byte)(activationOrdinal & 0xFF);
        offset += 2;
        PayloadCodec.WriteHash(payload, offset, deviceFingerprintHash);
        return payload;
    }
}
