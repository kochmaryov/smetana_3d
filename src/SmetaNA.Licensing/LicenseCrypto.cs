using System.Security.Cryptography;
using System.Text;

namespace SmetaNA.Licensing;

/// <summary>
/// Криптографические примитивы лицензирования: ECDSA P-256 + SHA-256 средствами встроенного .NET.
/// Внешние криптографические зависимости не используются.
/// </summary>
internal static class LicenseCrypto
{
    /// <summary>Размер подписи ECDSA P-256 в формате IEEE P1363 (r||s).</summary>
    public const int SignatureSize = 64;

    /// <summary>Создаёт новую пару ключей ECDSA P-256.</summary>
    public static ECDsa CreateSigningKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>Экспортирует приватный ключ в PEM (PKCS#8).</summary>
    public static string ExportPrivatePem(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>Экспортирует открытый ключ в PEM (SubjectPublicKeyInfo).</summary>
    public static string ExportPublicPem(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.ExportSubjectPublicKeyInfoPem();
    }

    /// <summary>Импортирует приватный ключ из PEM; владение ключом переходит вызывающему.</summary>
    public static ECDsa ImportPrivatePem(string pem) => ImportPem(pem, "приватного");

    /// <summary>Импортирует открытый ключ из PEM; владение ключом переходит вызывающему.</summary>
    public static ECDsa ImportPublicPem(string pem) => ImportPem(pem, "открытого");

    /// <summary>Подписывает данные ключом: в подписываемые байты добавляется домен формата.</summary>
    public static byte[] Sign(ECDsa key, string domain, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(domain);

        byte[] signature = key.SignData(DomainPrefixed(domain, payload), HashAlgorithmName.SHA256);
        if (signature.Length != SignatureSize)
        {
            throw new InvalidOperationException($"Неожиданный размер подписи ECDSA: {signature.Length}.");
        }

        return signature;
    }

    /// <summary>Проверяет подпись, созданную <see cref="Sign"/>.</summary>
    public static bool Verify(ECDsa key, string domain, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(domain);

        return signature.Length == SignatureSize
            && key.VerifyData(DomainPrefixed(domain, payload), signature, HashAlgorithmName.SHA256);
    }

    /// <summary>SHA-256 от данных.</summary>
    public static byte[] Sha256(ReadOnlySpan<byte> data) => SHA256.HashData(data);

    /// <summary>SHA-256 от UTF-8 представления строки.</summary>
    public static byte[] Sha256(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return SHA256.HashData(Encoding.UTF8.GetBytes(text));
    }

    private static ECDsa ImportPem(string pem, string keyRole)
    {
        ArgumentNullException.ThrowIfNull(pem);

        try
        {
            ECDsa key = ECDsa.Create();
            key.ImportFromPem(pem);
            return key;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new LicenseFormatException($"Некорректный PEM {keyRole} ключа.", ex);
        }
    }

    private static byte[] DomainPrefixed(string domain, ReadOnlySpan<byte> payload)
    {
        byte[] domainBytes = Encoding.UTF8.GetBytes(domain);
        var prefixed = new byte[domainBytes.Length + payload.Length];
        domainBytes.CopyTo(prefixed, 0);
        payload.CopyTo(prefixed.AsSpan(domainBytes.Length));
        return prefixed;
    }
}
