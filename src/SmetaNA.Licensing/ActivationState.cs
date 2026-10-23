using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>
/// Локальное состояние активации: применённый токен продавца (доверительный якорь, node-lock)
/// и рабочие поля (моменты активации и последней проверки, признак деактивации).
/// Хранится в подписанном локальным ключом файле — защита от простой подделки правкой текста.
/// </summary>
public sealed record ActivationState
{
    /// <summary>Версия формата.</summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>Ключ, по которому выполнена активация.</summary>
    public required Guid KeyId { get; init; }

    /// <summary>Применённый токен активации.</summary>
    public required Guid TokenId { get; init; }

    /// <summary>Строка токена активации (подписана продавцом).</summary>
    public required string TokenString { get; init; }

    /// <summary>Связывающий хеш устройства (node-lock).</summary>
    public required string DeviceFingerprintHash { get; init; }

    /// <summary>Момент активации (UTC).</summary>
    public required DateTimeOffset ActivatedAt { get; init; }

    /// <summary>Окончание действия активации; <c>null</c> — бессрочно.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>Момент последней успешной проверки (якорь против отката часов).</summary>
    public required DateTimeOffset LastValidatedAt { get; init; }

    /// <summary>Активация деактивирована на этом устройстве.</summary>
    public bool Deactivated { get; init; }

    /// <summary>Момент деактивации (UTC).</summary>
    public DateTimeOffset? DeactivatedAt { get; init; }

    /// <summary>Каноничные байты полезной нагрузки для подписи.</summary>
    internal byte[] ToPayloadBytes() => JsonSerializer.SerializeToUtf8Bytes(this, LicenseJson.Canonical);

    /// <summary>Восстанавливает состояние из байтов.</summary>
    internal static ActivationState FromPayloadBytes(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            ActivationState? state = JsonSerializer.Deserialize<ActivationState>(payload, LicenseJson.Canonical);
            return state ?? throw new LicenseStateException("Файл состояния активации повреждён: пустое содержимое.");
        }
        catch (JsonException ex)
        {
            throw new LicenseStateException("Файл состояния активации повреждён: некорректные данные.", ex);
        }
    }
}
