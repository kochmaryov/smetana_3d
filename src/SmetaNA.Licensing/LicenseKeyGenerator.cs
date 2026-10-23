using System.Security.Cryptography;

namespace SmetaNA.Licensing;

/// <summary>
/// Выпуск лицензионных ключей на стороне продавца: подписывает <see cref="LicenseKeyData"/> приватным ключом ECDSA P-256.
/// Приватный ключ хранится только у продавца (офлайн-инструмент выдачи); в приложение попадает лишь открытый ключ.
/// </summary>
public sealed class LicenseKeyGenerator : IDisposable
{
    private readonly ECDsa _signingKey;
    private readonly bool _ownsKey;

    /// <summary>Создаёт генератор с приватным ключом из PEM (PKCS#8); владение ключом принимается генератором.</summary>
    public LicenseKeyGenerator(string privateKeyPem)
    {
        _signingKey = LicenseCrypto.ImportPrivatePem(privateKeyPem);
        _ownsKey = true;
    }

    /// <summary>Создаёт генератор с готовым ключом; ключ остаётся во владении вызывающего.</summary>
    public LicenseKeyGenerator(ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _signingKey = signingKey;
        _ownsKey = false;
    }

    /// <summary>Создаёт новую пару ключей ECDSA P-256 для подписи лицензий.</summary>
    public static ECDsa CreateSigningKey() => LicenseCrypto.CreateSigningKey();

    /// <summary>Приватный ключ в PEM (PKCS#8) — для защищённого хранения у продавца.</summary>
    public string ExportPrivateKeyPem() => LicenseCrypto.ExportPrivatePem(_signingKey);

    /// <summary>Открытый ключ в PEM — встраивается в приложение.</summary>
    public string ExportPublicKeyPem() => LicenseCrypto.ExportPublicPem(_signingKey);

    /// <summary>Подписывает данные и собирает лицензионный ключ.</summary>
    public LicenseKey Generate(LicenseKeyData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        byte[] payload = data.ToPayloadBytes();
        byte[] signature = LicenseCrypto.Sign(_signingKey, LicenseKey.SignatureDomain, payload);
        return new LicenseKey(data, signature);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsKey)
        {
            _signingKey.Dispose();
        }
    }
}
