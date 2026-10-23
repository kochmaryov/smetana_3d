namespace SmetaNA.Licensing;

/// <summary>
/// Текстовый формат ключей и токенов: «ПРЕФИКС.сегмент[.сегмент…]», сегменты — Crockford Base32 с группировкой по 5 символов.
/// </summary>
internal static class LicenseText
{
    /// <summary>Собирает строку из префикса и бинарных сегментов.</summary>
    public static string Format(string prefix, params byte[][] segments)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(segments);

        var parts = new string[segments.Length + 1];
        parts[0] = prefix;
        for (int i = 0; i < segments.Length; i++)
        {
            parts[i + 1] = CrockfordBase32.Group(CrockfordBase32.Encode(segments[i]));
        }

        return string.Join('.', parts);
    }

    /// <summary>Разбирает строку и возвращает бинарные сегменты; префикс и число сегментов должны совпасть.</summary>
    public static byte[][] Split(string text, string expectedPrefix, int segmentCount)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(expectedPrefix);

        string[] parts = text.Trim().Split('.');
        if (parts.Length != segmentCount + 1)
        {
            throw new LicenseFormatException(
                $"Некорректный формат: ожидался префикс и сегментов — {segmentCount}, получено {parts.Length - 1}.");
        }

        if (!string.Equals(parts[0].Trim(), expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new LicenseFormatException($"Некорректный префикс «{parts[0].Trim()}»: ожидался «{expectedPrefix}».");
        }

        var segments = new byte[segmentCount][];
        for (int i = 0; i < segmentCount; i++)
        {
            segments[i] = CrockfordBase32.Decode(CrockfordBase32.Ungroup(parts[i + 1]));
        }

        return segments;
    }
}
