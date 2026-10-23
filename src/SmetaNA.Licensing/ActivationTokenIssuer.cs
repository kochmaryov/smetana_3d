using System.Security.Cryptography;

namespace SmetaNA.Licensing;

/// <summary>
/// Офлайн-инструмент продавца: выдаёт токены активации в обмен на запросы, следит за годовым лимитом активаций,
/// обрабатывает деактивации/переносы и выпускает подписанные списки отзывов.
/// Работает без сети: заявки приходят почтой/мессенджером, ответы уходят тем же каналом.
/// </summary>
public sealed class ActivationTokenIssuer : IDisposable
{
    private readonly ECDsa _signingKey;
    private readonly bool _ownsKey;
    private readonly LicenseKeyVerifier _keyVerifier;
    private readonly ActivationLedger _ledger;
    private readonly TimeProvider _time;
    private readonly TimeSpan? _tokenTtl;

    /// <summary>Создаёт инструмент с приватным ключом из PEM; владение ключом принимается инструментом.</summary>
    public ActivationTokenIssuer(
        string privateKeyPem,
        ActivationLedger ledger,
        TimeProvider? time = null,
        TimeSpan? tokenTtl = null)
    {
        _signingKey = LicenseCrypto.ImportPrivatePem(privateKeyPem);
        _ownsKey = true;
        _keyVerifier = new LicenseKeyVerifier(_signingKey);
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _time = time ?? TimeProvider.System;
        _tokenTtl = tokenTtl;
    }

    /// <summary>Создаёт инструмент с готовым ключом; ключ остаётся во владении вызывающего.</summary>
    public ActivationTokenIssuer(
        ECDsa signingKey,
        ActivationLedger ledger,
        TimeProvider? time = null,
        TimeSpan? tokenTtl = null)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _signingKey = signingKey;
        _ownsKey = false;
        _keyVerifier = new LicenseKeyVerifier(_signingKey);
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _time = time ?? TimeProvider.System;
        _tokenTtl = tokenTtl;
    }

    /// <summary>Журнал активаций, обслуживаемый инструментом.</summary>
    public ActivationLedger Ledger => _ledger;

    /// <summary>Выдаёт токен активации для устройства из запроса; проверяет ключ и годовой лимит.</summary>
    public ActivationIssueResult Issue(LicenseKey key, ActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = _time.GetUtcNow();

        LicenseKeyValidationResult keyCheck = _keyVerifier.Validate(key, now);
        if (!keyCheck.IsValid)
        {
            IssueProblem problem = keyCheck.Problem switch
            {
                LicenseKeyProblem.Expired => IssueProblem.ExpiredKey,
                _ => IssueProblem.InvalidKey,
            };

            return Failure(problem, keyCheck.Message);
        }

        if (request.KeyId != key.Data.KeyId)
        {
            return Failure(IssueProblem.KeyMismatch, "Запрос активации относится к другому ключу.");
        }

        int used = _ledger.CountActivations(key.Data.KeyId, now);
        if (used >= key.Data.MaxActivationsPerYear)
        {
            return Failure(
                IssueProblem.AnnualLimitExceeded,
                $"Достигнут лимит активаций в год: {used} из {key.Data.MaxActivationsPerYear}. " +
                "Лимит можно переносить вручную через поддержку (деактивация прежних устройств).");
        }

        DateTimeOffset? validUntil = key.Data.ExpiresAt;
        if (_tokenTtl is { } ttl && (validUntil is null || now + ttl < validUntil))
        {
            validUntil = now + ttl;
        }

        ActivationToken token = ActivationToken.Create(
            key.Data.KeyId,
            request.DeviceFingerprintHash,
            now,
            validUntil,
            used + 1,
            _signingKey);

        _ledger.AddActivation(key.Data.KeyId, token.TokenId, request.DeviceFingerprintHash, now);

        return new ActivationIssueResult
        {
            Success = true,
            Problem = IssueProblem.None,
            Message = "Токен активации выдан.",
            Token = token,
        };
    }

    /// <summary>Перенос лицензии: отзыв прежней активации и выдача токена новому устройству.</summary>
    /// <remarks>Перенос расходует квоту активаций в год — она рассчитана на защиту от передачи ключа другим людям.</remarks>
    public ActivationIssueResult Transfer(Guid revokedTokenId, LicenseKey key, ActivationRequest request, string? reason = "перенос лицензии")
    {
        _ledger.Revoke(revokedTokenId, reason);
        return Issue(key, request);
    }

    /// <summary>Помечает активацию отозванной по квитанции деактивации от клиента.</summary>
    public bool ProcessDeactivation(DeactivationReceipt receipt, string? reason = "деактивация по запросу пользователя")
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return _ledger.Revoke(receipt.TokenId, reason);
    }

    /// <summary>Помечает активацию отозванной напрямую.</summary>
    public bool Revoke(Guid tokenId, string? reason = null) => _ledger.Revoke(tokenId, reason);

    /// <summary>Собирает подписанный список отзывов для офлайн-доставки пользователям.</summary>
    public RevocationList CreateRevocationList() =>
        RevocationList.Create(_time.GetUtcNow(), _ledger.GetRevokedTokenIds(), _signingKey);

    /// <inheritdoc />
    public void Dispose()
    {
        _keyVerifier.Dispose();
        if (_ownsKey)
        {
            _signingKey.Dispose();
        }
    }

    private static ActivationIssueResult Failure(IssueProblem problem, string message) =>
        new()
        {
            Success = false,
            Problem = problem,
            Message = message,
            Token = null,
        };
}
