namespace SmetaNA.Licensing;

/// <summary>Состояние лицензии на устройстве.</summary>
public enum LicenseState
{
    /// <summary>Нет ни лицензии, ни триала.</summary>
    NotLicensed,

    /// <summary>Ключ действителен, но активация на этом устройстве не выполнена.</summary>
    NotActivated,

    /// <summary>Лицензия активна и действует.</summary>
    Licensed,

    /// <summary>Срок действия лицензии/активации истёк.</summary>
    LicenseExpired,

    /// <summary>Активация привязана к другому устройству.</summary>
    DeviceMismatch,

    /// <summary>Активация отозвана (деактивация или перенос).</summary>
    Revoked,

    /// <summary>Лицензия деактивирована на этом устройстве.</summary>
    Deactivated,

    /// <summary>Пробный период идёт.</summary>
    TrialActive,

    /// <summary>Пробный период истёк.</summary>
    TrialExpired,

    /// <summary>Лицензионный ключ недействителен.</summary>
    InvalidKey,

    /// <summary>Локальное состояние повреждено или подделано.</summary>
    InvalidState,
}

/// <summary>Итоговый статус лицензии для UI.</summary>
public sealed record LicenseStatus
{
    /// <summary>Состояние.</summary>
    public required LicenseState State { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }

    /// <summary>Окончание действия (если применимо).</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>Обнаружен откат системных часов.</summary>
    public bool ClockRollbackDetected { get; init; }

    /// <summary>Идентификатор ключа (если известен).</summary>
    public Guid? KeyId { get; init; }

    /// <summary>Идентификатор токена активации (если известен).</summary>
    public Guid? TokenId { get; init; }
}

/// <summary>Результат применения токена активации.</summary>
public sealed record ActivationResult
{
    /// <summary>Активация применена.</summary>
    public required bool Success { get; init; }

    /// <summary>Причина отказа.</summary>
    public required TokenProblem Problem { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }

    /// <summary>Окончание действия активации (если применимо).</summary>
    public DateTimeOffset? ValidUntil { get; init; }
}
