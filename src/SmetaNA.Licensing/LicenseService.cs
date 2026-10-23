namespace SmetaNA.Licensing;

/// <summary>
/// Клиентская часть лицензирования: запрос активации, применение токена, проверка статуса,
/// деактивация/перенос и пробный период. Работает полностью офлайн — все проверки локальные,
/// токен активации доставляется пользователю продавцом (почта/мессенджер/флешка).
/// </summary>
public sealed class LicenseService : IDisposable
{
    /// <summary>Допуск расхождения часов, при котором откат не считается подозрительным.</summary>
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(2);

    private readonly LocalStateStore _store;
    private readonly IDeviceFingerprintProvider _devices;
    private readonly TimeProvider _time;
    private readonly LicenseKeyVerifier _keyVerifier;
    private readonly ActivationTokenVerifier _tokenVerifier;
    private readonly TrialManager _trials;
    private bool _disposed;

    /// <summary>Создаёт сервис поверх локального хранилища состояния.</summary>
    public LicenseService(
        string vendorPublicKeyPem,
        string stateDirectory,
        IDeviceFingerprintProvider? devices = null,
        TimeProvider? time = null,
        TrialOptions? trialOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorPublicKeyPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);

        _store = new LocalStateStore(stateDirectory);
        _devices = devices ?? new DefaultDeviceFingerprintProvider();
        _time = time ?? TimeProvider.System;
        _keyVerifier = new LicenseKeyVerifier(vendorPublicKeyPem);
        _tokenVerifier = new ActivationTokenVerifier(vendorPublicKeyPem);
        _trials = new TrialManager(_store, _devices, _time, trialOptions);
    }

    /// <summary>Идентификатор текущего устройства (для поддержки и диагностики).</summary>
    public DeviceFingerprint GetDeviceFingerprint() => _devices.GetFingerprint();

    /// <summary>Формирует запрос активации для передачи продавцу.</summary>
    public string CreateActivationRequest(LicenseKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ThrowIfDisposed();

        LicenseKeyValidationResult check = _keyVerifier.Validate(key, _time.GetUtcNow());
        if (!check.IsValid)
        {
            throw new LicenseFormatException($"Нельзя создать запрос активации: {check.Message}");
        }

        ActivationRequest request = ActivationRequest.Create(
            key.Data.KeyId,
            _devices.GetFingerprint().BindingHash,
            _time.GetUtcNow());

        return request.ToRequestString();
    }

    /// <summary>
    /// Применяет токен активации, полученный от продавца: проверяет подпись, node-lock, сроки и отзывы,
    /// затем сохраняет подписанное локальное состояние. Работа продолжается без сети до окончания действия токена.
    /// </summary>
    public ActivationResult ApplyActivationToken(string tokenString, LicenseKey? key = null, RevocationList? revocations = null)
    {
        ArgumentNullException.ThrowIfNull(tokenString);
        ThrowIfDisposed();

        if (!ActivationToken.TryParse(tokenString, out ActivationToken? token) || token is null)
        {
            return Failure(TokenProblem.InvalidSignature, "Токен активации повреждён или имеет некорректный формат.");
        }

        TokenValidationResult check = _tokenVerifier.Validate(
            token,
            _devices.GetFingerprint().BindingHash,
            _time.GetUtcNow(),
            revocations,
            key);

        if (!check.IsValid)
        {
            return Failure(check.Problem, check.Message);
        }

        DateTimeOffset now = _time.GetUtcNow();
        var state = new ActivationState
        {
            KeyId = token.KeyId,
            TokenId = token.TokenId,
            TokenString = token.ToTokenString(),
            DeviceFingerprintHash = token.DeviceFingerprintHash,
            ActivatedAt = now,
            ValidUntil = token.ValidUntil,
            LastValidatedAt = now,
        };

        _store.SaveActivationState(state);

        return new ActivationResult
        {
            Success = true,
            Problem = TokenProblem.None,
            Message = "Лицензия активирована на этом устройстве.",
            ValidUntil = token.ValidUntil,
        };
    }

    /// <summary>Возвращает текущий статус лицензии; при проверке якорь времени продвигается вперёд.</summary>
    public LicenseStatus GetStatus(LicenseKey? key = null, RevocationList? revocations = null)
    {
        ThrowIfDisposed();

        DateTimeOffset now = _time.GetUtcNow();

        ActivationState? state;
        try
        {
            state = _store.LoadActivationState();
        }
        catch (LicenseStateException ex)
        {
            return new LicenseStatus
            {
                State = LicenseState.InvalidState,
                Message = ex.Message,
            };
        }

        if (state is not null)
        {
            return EvaluateActivation(state, now, key, revocations);
        }

        if (key is not null)
        {
            LicenseKeyValidationResult keyCheck = _keyVerifier.Validate(key, now);
            return new LicenseStatus
            {
                State = keyCheck.IsValid ? LicenseState.NotActivated : LicenseState.InvalidKey,
                Message = keyCheck.IsValid
                    ? "Лицензионный ключ действителен: требуется активация на этом устройстве."
                    : keyCheck.Message,
                ValidUntil = key.Data.ExpiresAt,
                KeyId = key.Data.KeyId,
            };
        }

        return EvaluateTrial();
    }

    /// <summary>Деактивирует лицензию на этом устройстве и возвращает квитанцию для продавца.</summary>
    /// <remarks>
    /// Квитанцию нужно передать продавцу (поддержке) — только после этого токен попадёт в список отзывов,
    /// и лимит активаций можно будет использовать для переноса на другое устройство.
    /// </remarks>
    public DeactivationReceipt Deactivate()
    {
        ThrowIfDisposed();

        ActivationState? state = _store.LoadActivationState()
            ?? throw new InvalidOperationException("Активация отсутствует: деактивировать нечего.");

        if (state.Deactivated)
        {
            throw new InvalidOperationException("Лицензия на этом устройстве уже деактивирована.");
        }

        DateTimeOffset now = _time.GetUtcNow();
        var receipt = new DeactivationReceipt(state.KeyId, state.TokenId, state.DeviceFingerprintHash, now);
        _store.SaveActivationState(state with
        {
            Deactivated = true,
            DeactivatedAt = now,
            LastValidatedAt = now,
        });

        return receipt;
    }

    /// <summary>Запускает пробный период (идемпотентно: повторный вызов не перезапускает триал).</summary>
    public TrialStatus StartTrial()
    {
        ThrowIfDisposed();
        return _trials.Start();
    }

    /// <summary>Проверяет пробный период.</summary>
    public TrialStatus GetTrialStatus()
    {
        ThrowIfDisposed();
        return _trials.GetStatus();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _keyVerifier.Dispose();
        _tokenVerifier.Dispose();
        _store.Dispose();
    }

    private LicenseStatus EvaluateActivation(
        ActivationState state,
        DateTimeOffset now,
        LicenseKey? key,
        RevocationList? revocations)
    {
        bool clockRollback = now + ClockTolerance < state.LastValidatedAt;
        DateTimeOffset effectiveNow = now > state.LastValidatedAt ? now : state.LastValidatedAt;

        if (state.Deactivated)
        {
            return new LicenseStatus
            {
                State = LicenseState.Deactivated,
                Message = "Лицензия деактивирована на этом устройстве.",
                ValidUntil = state.ValidUntil,
                ClockRollbackDetected = clockRollback,
                KeyId = state.KeyId,
                TokenId = state.TokenId,
            };
        }

        if (!ActivationToken.TryParse(state.TokenString, out ActivationToken? token) || token is null
            || token.TokenId != state.TokenId)
        {
            return InvalidState("Файл состояния активации повреждён.", state, clockRollback);
        }

        if (!string.Equals(token.DeviceFingerprintHash, _devices.GetFingerprint().BindingHash, StringComparison.Ordinal))
        {
            return new LicenseStatus
            {
                State = LicenseState.DeviceMismatch,
                Message = "Активация привязана к другому устройству.",
                ClockRollbackDetected = clockRollback,
                KeyId = state.KeyId,
                TokenId = state.TokenId,
            };
        }

        if (!_tokenVerifier.VerifySignature(token))
        {
            return InvalidState("Подпись токена активации недействительна: состояние повреждено.", state, clockRollback);
        }

        if (revocations?.IsRevoked(token.TokenId) == true)
        {
            return new LicenseStatus
            {
                State = LicenseState.Revoked,
                Message = "Эта активация отозвана (лицензия деактивирована или перенесена).",
                ClockRollbackDetected = clockRollback,
                KeyId = state.KeyId,
                TokenId = state.TokenId,
            };
        }

        DateTimeOffset? validUntil = token.ValidUntil;
        if (key is not null)
        {
            if (key.Data.KeyId != state.KeyId)
            {
                return InvalidState("Файл состояния активации относится к другому ключу.", state, clockRollback);
            }

            if (key.Data.ExpiresAt is { } keyExpiry)
            {
                validUntil = validUntil is { } tokenExpiry && tokenExpiry < keyExpiry ? tokenExpiry : keyExpiry;
            }
        }

        if (validUntil is { } expiry && effectiveNow >= expiry)
        {
            return new LicenseStatus
            {
                State = LicenseState.LicenseExpired,
                Message = $"Срок действия лицензии истёк {expiry:yyyy-MM-dd}.",
                ValidUntil = expiry,
                ClockRollbackDetected = clockRollback,
                KeyId = state.KeyId,
                TokenId = state.TokenId,
            };
        }

        if (now > state.LastValidatedAt)
        {
            _store.SaveActivationState(state with { LastValidatedAt = now });
        }

        return new LicenseStatus
        {
            State = LicenseState.Licensed,
            Message = "Лицензия активна.",
            ValidUntil = validUntil,
            ClockRollbackDetected = clockRollback,
            KeyId = state.KeyId,
            TokenId = state.TokenId,
        };
    }

    private LicenseStatus EvaluateTrial()
    {
        TrialStatus trial = _trials.GetStatus();
        return trial.Kind switch
        {
            TrialStatusKind.Active => new LicenseStatus
            {
                State = LicenseState.TrialActive,
                Message = trial.Message,
                ValidUntil = trial.ExpiresAt,
                ClockRollbackDetected = trial.ClockRollbackDetected,
            },
            TrialStatusKind.Expired => new LicenseStatus
            {
                State = LicenseState.TrialExpired,
                Message = trial.Message,
                ValidUntil = trial.ExpiresAt,
                ClockRollbackDetected = trial.ClockRollbackDetected,
            },
            TrialStatusKind.Tampered or TrialStatusKind.DeviceMismatch => new LicenseStatus
            {
                State = LicenseState.InvalidState,
                Message = trial.Message,
                ClockRollbackDetected = trial.ClockRollbackDetected,
            },
            _ => new LicenseStatus
            {
                State = LicenseState.NotLicensed,
                Message = "Лицензия отсутствует; доступен пробный период.",
            },
        };
    }

    private static LicenseStatus InvalidState(string message, ActivationState state, bool clockRollback) =>
        new()
        {
            State = LicenseState.InvalidState,
            Message = message,
            ClockRollbackDetected = clockRollback,
            KeyId = state.KeyId,
            TokenId = state.TokenId,
        };

    private static ActivationResult Failure(TokenProblem problem, string message) =>
        new()
        {
            Success = false,
            Problem = problem,
            Message = message,
        };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
