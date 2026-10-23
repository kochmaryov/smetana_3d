using System.Security.Cryptography;
using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>
/// Подписанный продавцом список отозванных токенов активации (деактивации и переносы).
/// Доставляется офлайн: с обновлением, по почте или на флешке. Формат — JSON-конверт <see cref="SignedEnvelope"/>.
/// </summary>
public sealed class RevocationList
{
    /// <summary>Имя формата конверта.</summary>
    public const string EnvelopeFormat = "smetana-revocation/1";

    /// <summary>Домен подписи — защищает от подмены формата данных.</summary>
    internal const string SignatureDomain = "SmetaNA.Licensing.RevocationList/v1";

    private sealed record RevocationPayload
    {
        public DateTimeOffset GeneratedAt { get; init; }
        public Guid[] TokenIds { get; init; } = [];
    }

    private readonly byte[] _payload;
    private readonly byte[] _signature;

    private RevocationList(byte[] payload, byte[] signature, DateTimeOffset generatedAt, IReadOnlyList<Guid> tokenIds)
    {
        _payload = payload;
        _signature = signature;
        GeneratedAt = generatedAt;
        RevokedTokenIds = tokenIds;
    }

    /// <summary>Момент формирования списка (UTC).</summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>Отозванные идентификаторы токенов активации.</summary>
    public IReadOnlyList<Guid> RevokedTokenIds { get; }

    /// <summary>Создаёт подписанный список (сторона продавца).</summary>
    public static RevocationList Create(DateTimeOffset generatedAt, IEnumerable<Guid> revokedTokenIds, ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(revokedTokenIds);
        ArgumentNullException.ThrowIfNull(signingKey);

        var payloadModel = new RevocationPayload
        {
            GeneratedAt = generatedAt.ToUniversalTime(),
            TokenIds = [.. revokedTokenIds],
        };

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(payloadModel, LicenseJson.Canonical);
        byte[] signature = LicenseCrypto.Sign(signingKey, SignatureDomain, payload);
        return new RevocationList(payload, signature, generatedAt, payloadModel.TokenIds);
    }

    /// <summary>Отозван ли токен.</summary>
    public bool IsRevoked(Guid tokenId) => RevokedTokenIds.Contains(tokenId);

    /// <summary>Текст конверта для передачи пользователям.</summary>
    public string ToEnvelopeString() => SignedEnvelope.Write(EnvelopeFormat, _payload, _signature);

    /// <inheritdoc />
    public override string ToString() => ToEnvelopeString();

    /// <summary>Разбирает конверт и проверяет подпись открытым ключом продавца.</summary>
    public static RevocationList Parse(string envelopeText, string vendorPublicKeyPem)
    {
        ArgumentNullException.ThrowIfNull(envelopeText);
        ArgumentNullException.ThrowIfNull(vendorPublicKeyPem);

        using ECDsa publicKey = LicenseCrypto.ImportPublicPem(vendorPublicKeyPem);
        return Parse(envelopeText, publicKey);
    }

    /// <summary>Разбирает конверт и проверяет подпись открытым ключом продавца; ключ остаётся во владении вызывающего.</summary>
    public static RevocationList Parse(string envelopeText, ECDsa vendorPublicKey)
    {
        ArgumentNullException.ThrowIfNull(envelopeText);
        ArgumentNullException.ThrowIfNull(vendorPublicKey);

        (byte[] payload, byte[] signature) = SignedEnvelope.Read(envelopeText, EnvelopeFormat);
        if (!LicenseCrypto.Verify(vendorPublicKey, SignatureDomain, payload, signature))
        {
            throw new LicenseFormatException("Список отзывов повреждён или подделан: подпись недействительна.");
        }

        RevocationPayload? payloadModel;
        try
        {
            payloadModel = JsonSerializer.Deserialize<RevocationPayload>(payload, LicenseJson.Canonical);
        }
        catch (JsonException ex)
        {
            throw new LicenseFormatException("Список отзывов повреждён: некорректная полезная нагрузка.", ex);
        }

        if (payloadModel is null)
        {
            throw new LicenseFormatException("Список отзывов повреждён: пустая полезная нагрузка.");
        }

        return new RevocationList(payload, signature, payloadModel.GeneratedAt, payloadModel.TokenIds);
    }
}
