using System.Globalization;
using System.Text;

namespace SmetaNA.Licensing;

/// <summary>Один источник данных для идентификатора устройства.</summary>
/// <param name="Name">Имя источника (латиницей, стабильное).</param>
/// <param name="Value">Значение источника.</param>
public sealed record DeviceFingerprintComponent(string Name, string Value);

/// <summary>
/// Идентификатор устройства: нормализованный набор источников и его криптографический хеш.
/// Хеш используется для node-lock: токен активации привязывается к <see cref="BindingHash"/>.
/// </summary>
public sealed class DeviceFingerprint
{
    /// <summary>Версия алгоритма нормализации и хеширования.</summary>
    public const string Algorithm = "smetana-fp/v1";

    /// <summary>Размер «связывающего» хеша в байтах (достаточно против подбора).</summary>
    public const int BindingHashSize = 16;

    private DeviceFingerprint(IReadOnlyList<DeviceFingerprintComponent> components, byte[] hash)
    {
        Components = components;
        HashBytes = hash;
    }

    /// <summary>Упорядоченные источники, вошедшие в хеш.</summary>
    public IReadOnlyList<DeviceFingerprintComponent> Components { get; }

    /// <summary>SHA-256 каноничного представления (32 байта).</summary>
    public byte[] HashBytes { get; }

    /// <summary>SHA-256 каноничного представления в hex (64 символа).</summary>
    public string Hash => Convert.ToHexString(HashBytes);

    /// <summary>
    /// Связывающий хеш для node-lock: первые 16 байт <see cref="Hash"/>, hex (32 символа).
    /// Усечение до 128 бит не ослабляет привязку (поиск коллизии ~2^128), но делает компактнее токены.
    /// </summary>
    public string BindingHash => Convert.ToHexString(HashBytes.AsSpan(0, BindingHashSize));

    /// <summary>Собирает идентификатор из источников: сортировка по имени, значения нормализуются.</summary>
    public static DeviceFingerprint Create(IEnumerable<DeviceFingerprintComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var normalized = components
            .Select(component => new DeviceFingerprintComponent(
                component.Name.Trim().ToLowerInvariant(),
                component.Value.Trim().ToUpperInvariant()))
            .OrderBy(component => component.Name, StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            throw new ArgumentException("Для идентификатора устройства нужен хотя бы один источник.", nameof(components));
        }

        var canonical = new StringBuilder(Algorithm);
        canonical.Append('\n');
        foreach (DeviceFingerprintComponent component in normalized)
        {
            canonical.Append(component.Name).Append('=').Append(component.Value).Append('\n');
        }

        return new DeviceFingerprint(normalized, LicenseCrypto.Sha256(canonical.ToString()));
    }

    /// <summary>Нормализует строку связывающего хеша: верхний регистр, проверка длины.</summary>
    public static string NormalizeBindingHash(string bindingHash)
    {
        ArgumentNullException.ThrowIfNull(bindingHash);

        string normalized = bindingHash.Trim().ToUpperInvariant();
        if (normalized.Length != BindingHashSize * 2)
        {
            throw new ArgumentException(
                $"Связывающий хеш устройства должен быть {BindingHashSize * 2} hex-символов.",
                nameof(bindingHash));
        }

        return normalized;
    }

    /// <summary>Преобразует связывающий хеш из hex в байты.</summary>
    public static byte[] BindingHashToBytes(string bindingHash) =>
        Convert.FromHexString(NormalizeBindingHash(bindingHash));

    /// <summary>Преобразует связывающий хеш из байт в hex.</summary>
    public static string BindingHashFromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != BindingHashSize)
        {
            throw new ArgumentException(
                $"Связывающий хеш устройства должен быть {BindingHashSize} байт.",
                nameof(bytes));
        }

        return Convert.ToHexString(bytes);
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"device-fingerprint/{BindingHash}");
}
