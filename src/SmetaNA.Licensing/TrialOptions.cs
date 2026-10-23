namespace SmetaNA.Licensing;

/// <summary>Настройки пробного периода.</summary>
public sealed record TrialOptions
{
    /// <summary>Длительность пробного периода (по умолчанию 14 дней).</summary>
    public TimeSpan Length { get; init; } = TimeSpan.FromDays(14);
}

/// <summary>Состояние пробного периода.</summary>
public enum TrialStatusKind
{
    /// <summary>Триал ещё не запускался.</summary>
    NotStarted,

    /// <summary>Триал идёт.</summary>
    Active,

    /// <summary>Триал честно истёк.</summary>
    Expired,

    /// <summary>Файл состояния триала повреждён или подделан.</summary>
    Tampered,

    /// <summary>Файл состояния триала относится к другому устройству.</summary>
    DeviceMismatch,
}

/// <summary>Результат проверки пробного периода.</summary>
public sealed record TrialStatus
{
    /// <summary>Состояние.</summary>
    public required TrialStatusKind Kind { get; init; }

    /// <summary>Момент окончания триала (если известен).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Обнаружен откат системных часов.</summary>
    public bool ClockRollbackDetected { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }
}
