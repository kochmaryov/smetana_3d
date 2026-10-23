using System.Buffers.Binary;

namespace SmetaNA.Licensing;

/// <summary>Кодирование фиксированных полей бинарных полезных нагрузок ключей и токенов.</summary>
internal static class PayloadCodec
{
    /// <summary>Размер GUID в байтах.</summary>
    public const int GuidSize = 16;

    /// <summary>Разбор временной метки Unix.</summary>
    public const int TimeSize = 4;

    /// <summary>Размер связывающего хеша устройства.</summary>
    public const int HashSize = DeviceFingerprint.BindingHashSize;

    private const long MinUnixSeconds = 946684800; // 2000-01-01T00:00:00Z
    private const long MaxUnixSeconds = 4294967295; // 2106-02-07T06:28:15Z

    /// <summary>Записывает GUID в каноничном (hex «N») порядке байт.</summary>
    public static void WriteGuid(byte[] buffer, int offset, Guid value)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Convert.FromHexString(value.ToString("N")).CopyTo(buffer, offset);
    }

    /// <summary>Читает GUID, записанный <see cref="WriteGuid"/>.</summary>
    public static Guid ReadGuid(byte[] buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Guid.ParseExact(Convert.ToHexString(buffer.AsSpan(offset, GuidSize)).ToLowerInvariant(), "N");
    }

    /// <summary>Записывает временную метку Unix (uint32, старший байт первый).</summary>
    public static void WriteTime(byte[] buffer, int offset, DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, TimeSize), ToUnixSeconds(value));
    }

    /// <summary>Читает временную метку Unix.</summary>
    public static DateTimeOffset ReadTime(byte[] buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, TimeSize)));
    }

    /// <summary>Записывает необязательную метку: <c>null</c> кодируется нулём.</summary>
    public static void WriteOptionalTime(byte[] buffer, int offset, DateTimeOffset? value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(
            buffer.AsSpan(offset, TimeSize),
            value is { } time ? ToUnixSeconds(time) : 0u);
    }

    /// <summary>Читает необязательную метку: ноль означает отсутствие значения.</summary>
    public static DateTimeOffset? ReadOptionalTime(byte[] buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        uint seconds = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, TimeSize));
        return seconds == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    /// <summary>Записывает связывающий хеш устройства (hex → байты).</summary>
    public static void WriteHash(byte[] buffer, int offset, string bindingHash)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        DeviceFingerprint.BindingHashToBytes(bindingHash).CopyTo(buffer, offset);
    }

    /// <summary>Читает связывающий хеш устройства (байты → hex).</summary>
    public static string ReadHash(byte[] buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return DeviceFingerprint.BindingHashFromBytes(buffer.AsSpan(offset, HashSize).ToArray());
    }

    private static uint ToUnixSeconds(DateTimeOffset value)
    {
        long seconds = value.ToUnixTimeSeconds();
        if (seconds < MinUnixSeconds || seconds > MaxUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Дата должна попадать в диапазон 2000..2106 годов.");
        }

        return (uint)seconds;
    }
}
