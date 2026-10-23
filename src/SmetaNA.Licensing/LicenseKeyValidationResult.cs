namespace SmetaNA.Licensing;

/// <summary>Результат проверки лицензионного ключа.</summary>
public sealed record LicenseKeyValidationResult
{
    /// <summary>Ключ прошёл все проверки.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Обнаруженная проблема.</summary>
    public required LicenseKeyProblem Problem { get; init; }

    /// <summary>Человекочитаемое пояснение.</summary>
    public required string Message { get; init; }
}

/// <summary>Причины, по которым лицензионный ключ может быть отвергнут.</summary>
public enum LicenseKeyProblem
{
    /// <summary>Проблем нет.</summary>
    None,

    /// <summary>Подпись не прошла проверку (ключ подделан или повреждён).</summary>
    InvalidSignature,

    /// <summary>Ключ выдан в будущем.</summary>
    NotYetValid,

    /// <summary>Срок действия ключа истёк.</summary>
    Expired,

    /// <summary>Ключ выдан на другого лицензиата.</summary>
    LicenseeMismatch,
}
