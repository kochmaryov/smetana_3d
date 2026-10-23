using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>
/// Локальное состояние пробного периода: моменты начала, окончания и последней проверки плюс привязка к устройству.
/// Хранится в подписанном локальным ключом файле; откат часов не продлевает триал — см. <see cref="TrialManager"/>.
/// </summary>
public sealed record TrialState
{
    /// <summary>Версия формата.</summary>
    public int FormatVersion { get; init; } = 1;

    /// <summary>Момент начала пробного периода (UTC).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Момент окончания пробного периода (UTC).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Момент последней проверки (якорь против отката часов).</summary>
    public required DateTimeOffset LastValidatedAt { get; init; }

    /// <summary>Связывающий хеш устройства, на котором запущен триал.</summary>
    public required string DeviceFingerprintHash { get; init; }

    /// <summary>Каноничные байты полезной нагрузки для подписи.</summary>
    internal byte[] ToPayloadBytes() => JsonSerializer.SerializeToUtf8Bytes(this, LicenseJson.Canonical);

    /// <summary>Восстанавливает состояние из байтов.</summary>
    internal static TrialState FromPayloadBytes(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            TrialState? state = JsonSerializer.Deserialize<TrialState>(payload, LicenseJson.Canonical);
            return state ?? throw new LicenseStateException("Файл состояния триала повреждён: пустое содержимое.");
        }
        catch (JsonException ex)
        {
            throw new LicenseStateException("Файл состояния триала повреждён: некорректные данные.", ex);
        }
    }
}
