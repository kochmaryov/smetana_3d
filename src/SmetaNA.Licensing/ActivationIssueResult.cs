namespace SmetaNA.Licensing;

/// <summary>Причины отказа в выдаче токена активации.</summary>
public enum IssueProblem
{
    /// <summary>Отказа нет.</summary>
    None,

    /// <summary>Подпись ключа недействительна.</summary>
    InvalidKey,

    /// <summary>Срок действия ключа истёк.</summary>
    ExpiredKey,

    /// <summary>Запрос относится к другому ключу.</summary>
    KeyMismatch,

    /// <summary>Исчерпан лимит активаций в год.</summary>
    AnnualLimitExceeded,
}

/// <summary>Результат выдачи токена активации.</summary>
public sealed record ActivationIssueResult
{
    /// <summary>Токен выдан.</summary>
    public required bool Success { get; init; }

    /// <summary>Причина отказа.</summary>
    public required IssueProblem Problem { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }

    /// <summary>Выданный токен (если выдача прошла).</summary>
    public ActivationToken? Token { get; init; }
}
