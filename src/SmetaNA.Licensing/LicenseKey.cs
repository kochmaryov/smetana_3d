using System.Diagnostics.CodeAnalysis;

namespace SmetaNA.Licensing;

/// <summary>
/// Лицензионный ключ: подписанные данные (см. <see cref="LicenseKeyData"/>) и подпись ECDSA P-256.
/// Текстовый формат: <c>SMN1.полезная-нагрузка.подпись</c>, сегменты — Crockford Base32 с группировкой по 5 символов.
/// Ключ переносится копированием из письма о покупке; ручной ввод не предполагается.
/// </summary>
public sealed class LicenseKey
{
    /// <summary>Префикс формата ключа версии 1.</summary>
    public const string KeyPrefix = "SMN1";

    /// <summary>Домен подписи — защищает от подмены формата данных.</summary>
    internal const string SignatureDomain = "SmetaNA.Licensing.LicenseKey/v1";

    private readonly byte[] _signature;

    internal LicenseKey(LicenseKeyData data, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        if (signature.Length != LicenseCrypto.SignatureSize)
        {
            throw new ArgumentException($"Подпись должна быть {LicenseCrypto.SignatureSize} байт.", nameof(signature));
        }

        Data = data;
        _signature = [.. signature];
    }

    /// <summary>Данные ключа.</summary>
    public LicenseKeyData Data { get; }

    /// <summary>Подпись ECDSA P-256 (64 байта, r||s) над каноничной полезной нагрузкой.</summary>
    public byte[] Signature => [.. _signature];

    /// <summary>Подпись без копирования — для внутренней проверки.</summary>
    internal byte[] SignatureCore => _signature;

    /// <summary>Каноничная строка ключа.</summary>
    public string ToLicenseString() => LicenseText.Format(KeyPrefix, Data.ToPayloadBytes(), _signature);

    /// <inheritdoc />
    public override string ToString() => ToLicenseString();

    /// <summary>Разбирает строку ключа; формат должен быть корректным, подпись не проверяется.</summary>
    public static LicenseKey Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[][] segments = LicenseText.Split(text, KeyPrefix, 2);
        if (segments[0].Length != LicenseKeyData.PayloadSize)
        {
            throw new LicenseFormatException(
                $"Некорректная длина полезной нагрузки ключа: {segments[0].Length}.");
        }

        if (segments[1].Length != LicenseCrypto.SignatureSize)
        {
            throw new LicenseFormatException(
                $"Некорректная длина подписи ключа: {segments[1].Length}.");
        }

        LicenseKeyData data = LicenseKeyData.FromPayloadBytes(segments[0]);
        return new LicenseKey(data, segments[1]);
    }

    /// <summary>Пытается разобрать строку ключа; подпись не проверяется.</summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out LicenseKey? key)
    {
        try
        {
            key = Parse(text);
            return true;
        }
        catch (LicenseFormatException)
        {
            key = null;
            return false;
        }
    }
}
