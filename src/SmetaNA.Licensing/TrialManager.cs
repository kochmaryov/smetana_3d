namespace SmetaNA.Licensing;

/// <summary>
/// Пробный период с честным истечением: старт фиксируется в подписанном локальном файле, а
/// последующие проверки продвигают «якорь» времени только вперёд. Откат системных часов назад
/// не продлевает триал — срок считается от максимального уже виденного момента.
/// </summary>
/// <remarks>
/// Честность ограничена моделью угроз «не DRM»: пользователь с полным доступом к машине может удалить
/// файл состояния (и локальный ключ) и начать триал заново. Механизм защищает от случайного и простого
/// обхода (правка дат в файле, откат часов), а не от подготовленного взлома.
/// </remarks>
public sealed class TrialManager
{
    /// <summary>Допуск расхождения часов, при котором откат не считается подозрительным.</summary>
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(2);

    private readonly LocalStateStore _store;
    private readonly IDeviceFingerprintProvider _devices;
    private readonly TimeProvider _time;
    private readonly TrialOptions _options;

    /// <summary>Создаёт менеджер триала поверх локального хранилища.</summary>
    public TrialManager(
        LocalStateStore store,
        IDeviceFingerprintProvider devices,
        TimeProvider? time = null,
        TrialOptions? options = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _time = time ?? TimeProvider.System;
        _options = options ?? new TrialOptions();

        if (_options.Length <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Длительность триала должна быть положительной.");
        }
    }

    /// <summary>Запускает пробный период; повторный запуск не перезапускает и не продлевает триал.</summary>
    public TrialStatus Start()
    {
        DateTimeOffset now = _time.GetUtcNow();

        TrialState? existing;
        try
        {
            existing = _store.LoadTrialState();
        }
        catch (LicenseStateException ex)
        {
            return new TrialStatus
            {
                Kind = TrialStatusKind.Tampered,
                ClockRollbackDetected = false,
                Message = ex.Message,
            };
        }

        if (existing is not null)
        {
            return Evaluate(existing, now);
        }

        string deviceHash = _devices.GetFingerprint().BindingHash;
        var state = new TrialState
        {
            StartedAt = now,
            ExpiresAt = now + _options.Length,
            LastValidatedAt = now,
            DeviceFingerprintHash = deviceHash,
        };

        _store.SaveTrialState(state);
        return new TrialStatus
        {
            Kind = TrialStatusKind.Active,
            ExpiresAt = state.ExpiresAt,
            ClockRollbackDetected = false,
            Message = $"Пробный период запущен до {state.ExpiresAt:yyyy-MM-dd}.",
        };
    }

    /// <summary>Проверяет пробный период; при проверке якорь времени продвигается вперёд.</summary>
    public TrialStatus GetStatus()
    {
        TrialState? state;
        try
        {
            state = _store.LoadTrialState();
        }
        catch (LicenseStateException ex)
        {
            return new TrialStatus
            {
                Kind = TrialStatusKind.Tampered,
                ClockRollbackDetected = false,
                Message = ex.Message,
            };
        }

        if (state is null)
        {
            return new TrialStatus
            {
                Kind = TrialStatusKind.NotStarted,
                Message = "Пробный период ещё не запускался.",
            };
        }

        return Evaluate(state, _time.GetUtcNow());
    }

    private TrialStatus Evaluate(TrialState state, DateTimeOffset now)
    {
        string deviceHash = _devices.GetFingerprint().BindingHash;
        if (!string.Equals(state.DeviceFingerprintHash, deviceHash, StringComparison.Ordinal))
        {
            return new TrialStatus
            {
                Kind = TrialStatusKind.DeviceMismatch,
                ExpiresAt = state.ExpiresAt,
                Message = "Пробный период запущен на другом устройстве.",
            };
        }

        bool clockRollback = now + ClockTolerance < state.LastValidatedAt;
        DateTimeOffset effectiveNow = now > state.LastValidatedAt ? now : state.LastValidatedAt;

        if (now > state.LastValidatedAt)
        {
            _store.SaveTrialState(state with { LastValidatedAt = now });
        }

        if (effectiveNow >= state.ExpiresAt)
        {
            return new TrialStatus
            {
                Kind = TrialStatusKind.Expired,
                ExpiresAt = state.ExpiresAt,
                ClockRollbackDetected = clockRollback,
                Message = $"Пробный период истёк {state.ExpiresAt:yyyy-MM-dd}.",
            };
        }

        return new TrialStatus
        {
            Kind = TrialStatusKind.Active,
            ExpiresAt = state.ExpiresAt,
            ClockRollbackDetected = clockRollback,
            Message = $"Пробный период действует до {state.ExpiresAt:yyyy-MM-dd}.",
        };
    }
}
