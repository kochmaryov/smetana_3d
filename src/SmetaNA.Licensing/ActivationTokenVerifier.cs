using System.Security.Cryptography;

namespace SmetaNA.Licensing;

/// <summary>Причины, по которым токен активации может быть отвергнут.</summary>
public enum TokenProblem
{
    /// <summary>Проблем нет.</summary>
    None,

    /// <summary>Подпись недействительна (токен подделан или повреждён).</summary>
    InvalidSignature,

    /// <summary>Токен привязан к другому устройству (node-lock).</summary>
    DeviceMismatch,

    /// <summary>Токен выдан в будущем.</summary>
    NotYetValid,

    /// <summary>Срок действия токена истёк.</summary>
    Expired,

    /// <summary>Активация отозвана (деактивация/перенос).</summary>
    Revoked,

    /// <summary>Токен выдан для другого ключа.</summary>
    KeyMismatch,

    /// <summary>Срок действия ключа истёк.</summary>
    KeyExpired,

    /// <summary>Ключ недействителен (подпись).</summary>
    InvalidKey,
}

/// <summary>Результат проверки токена активации.</summary>
public sealed record TokenValidationResult
{
    /// <summary>Токен прошёл все проверки.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Обнаруженная проблема.</summary>
    public required TokenProblem Problem { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Проверка токенов активации на стороне приложения: подпись продавца, node-lock (совпадение устройства),
/// окно действия и (если доступен) список отзывов.
/// </summary>
public sealed class ActivationTokenVerifier : IDisposable
{
    /// <summary>Допуск расхождения часов для проверки «токен ещё не действует».</summary>
    private static readonly TimeSpan NotYetValidTolerance = TimeSpan.FromDays(1);

    private readonly ECDsa _publicKey;
    private readonly bool _ownsKey;

    /// <summary>Создаёт верификатор с открытым ключом продавца из PEM.</summary>
    public ActivationTokenVerifier(string publicKeyPem)
    {
        _publicKey = LicenseCrypto.ImportPublicPem(publicKeyPem);
        _ownsKey = true;
    }

    /// <summary>Создаёт верификатор с готовым открытым ключом; ключ остаётся во владении вызывающего.</summary>
    public ActivationTokenVerifier(ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        _publicKey = publicKey;
        _ownsKey = false;
    }

    /// <summary>Проверяет только подпись токена.</summary>
    public bool VerifySignature(ActivationToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return LicenseCrypto.Verify(_publicKey, ActivationToken.SignatureDomain, token.ToPayloadBytes(), token.SignatureCore);
    }

    /// <summary>
    /// Проверяет токен для устройства <paramref name="deviceFingerprintHash"/> на момент <paramref name="now"/>.
    /// Если передан <paramref name="key"/>, дополнительно сверяются идентификатор и срок действия ключа.
    /// </summary>
    public TokenValidationResult Validate(
        ActivationToken token,
        string deviceFingerprintHash,
        DateTimeOffset now,
        RevocationList? revocations = null,
        LicenseKey? key = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(deviceFingerprintHash);

        if (!VerifySignature(token))
        {
            return Failure(
                TokenProblem.InvalidSignature,
                "Подпись токена активации недействительна: токен подделан или повреждён.");
        }

        string currentHash = DeviceFingerprint.NormalizeBindingHash(deviceFingerprintHash);
        if (!string.Equals(token.DeviceFingerprintHash, currentHash, StringComparison.Ordinal))
        {
            return Failure(
                TokenProblem.DeviceMismatch,
                "Активация привязана к другому устройству.");
        }

        if (revocations?.IsRevoked(token.TokenId) == true)
        {
            return Failure(
                TokenProblem.Revoked,
                "Эта активация отозвана (лицензия деактивирована или перенесена).");
        }

        if (token.IssuedAt > now + NotYetValidTolerance)
        {
            return Failure(
                TokenProblem.NotYetValid,
                "Токен активации выдан в будущем: проверьте дату и время на устройстве.");
        }

        if (token.ValidUntil is { } validUntil && now >= validUntil)
        {
            return Failure(
                TokenProblem.Expired,
                $"Срок действия активации истёк {validUntil:yyyy-MM-dd}.");
        }

        if (key is not null)
        {
            if (key.Data.KeyId != token.KeyId)
            {
                return Failure(TokenProblem.KeyMismatch, "Токен активации выдан для другого ключа.");
            }

            if (key.Data.ExpiresAt is { } keyExpiry && now >= keyExpiry)
            {
                return Failure(TokenProblem.KeyExpired, $"Срок действия ключа истёк {keyExpiry:yyyy-MM-dd}.");
            }

            if (!LicenseCrypto.Verify(_publicKey, LicenseKey.SignatureDomain, key.Data.ToPayloadBytes(), key.SignatureCore))
            {
                return Failure(TokenProblem.InvalidKey, "Подпись лицензионного ключа недействительна.");
            }
        }

        return new TokenValidationResult
        {
            IsValid = true,
            Problem = TokenProblem.None,
            Message = "Активация действительна.",
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

    private static TokenValidationResult Failure(TokenProblem problem, string message) =>
        new()
        {
            IsValid = false,
            Problem = problem,
            Message = message,
        };
}
