using System.Security.Cryptography;

namespace SmetaNA.Licensing;

/// <summary>
/// Проверка лицензионных ключей на стороне приложения: подпись ECDSA P-256 открытым ключом продавца
/// и бизнес-правила (даты действия, закрепление за лицензиатом).
/// </summary>
public sealed class LicenseKeyVerifier : IDisposable
{
    /// <summary>Допуск расхождения часов для проверки «ключ ещё не действует».</summary>
    private static readonly TimeSpan NotYetValidTolerance = TimeSpan.FromDays(1);

    private readonly ECDsa _publicKey;
    private readonly bool _ownsKey;

    /// <summary>Создаёт верификатор с открытым ключом продавца из PEM (SubjectPublicKeyInfo).</summary>
    public LicenseKeyVerifier(string publicKeyPem)
    {
        _publicKey = LicenseCrypto.ImportPublicPem(publicKeyPem);
        _ownsKey = true;
    }

    /// <summary>Создаёт верификатор с готовым открытым ключом; ключ остаётся во владении вызывающего.</summary>
    public LicenseKeyVerifier(ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        _publicKey = publicKey;
        _ownsKey = false;
    }

    /// <summary>Открытый ключ в PEM.</summary>
    public string ExportPublicKeyPem() => LicenseCrypto.ExportPublicPem(_publicKey);

    /// <summary>Проверяет только подпись ключа.</summary>
    public bool VerifySignature(LicenseKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return LicenseCrypto.Verify(_publicKey, LicenseKey.SignatureDomain, key.Data.ToPayloadBytes(), key.SignatureCore);
    }

    /// <summary>
    /// Проверяет подпись и правила действия ключа на момент <paramref name="now"/>.
    /// Если <paramref name="licensee"/> задан, дополнительно сверяется хеш лицензиата.
    /// </summary>
    public LicenseKeyValidationResult Validate(LicenseKey key, DateTimeOffset now, string? licensee = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!VerifySignature(key))
        {
            return Failure(
                LicenseKeyProblem.InvalidSignature,
                "Подпись лицензионного ключа недействительна: ключ подделан или повреждён.");
        }

        if (key.Data.IssuedAt > now + NotYetValidTolerance)
        {
            return Failure(
                LicenseKeyProblem.NotYetValid,
                "Ключ выдан в будущем: проверьте дату и время на устройстве.");
        }

        if (key.Data.ExpiresAt is { } expiresAt && now >= expiresAt)
        {
            return Failure(
                LicenseKeyProblem.Expired,
                $"Срок действия ключа истёк {expiresAt:yyyy-MM-dd}.");
        }

        if (licensee is not null && !key.Data.MatchesLicensee(licensee))
        {
            return Failure(
                LicenseKeyProblem.LicenseeMismatch,
                "Ключ выдан на другого лицензиата.");
        }

        return new LicenseKeyValidationResult
        {
            IsValid = true,
            Problem = LicenseKeyProblem.None,
            Message = "Лицензионный ключ действителен.",
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsKey)
        {
            _publicKey.Dispose();
        }
    }

    private static LicenseKeyValidationResult Failure(LicenseKeyProblem problem, string message) =>
        new()
        {
            IsValid = false,
            Problem = problem,
            Message = message,
        };
}
