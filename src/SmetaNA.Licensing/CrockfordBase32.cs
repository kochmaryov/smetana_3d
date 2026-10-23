using System.Text;

namespace SmetaNA.Licensing;

/// <summary>
/// Base32 в алфавите Crockford: без регистра-зависимости и без похожих символов (O/0, I/1/L).
/// Применяется для текстового представления ключей и токенов — их можно диктовать и переносить вручную.
/// </summary>
internal static class CrockfordBase32
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Кодирует байты в Base32 без дополнения.</summary>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                builder.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return builder.ToString();
    }

    /// <summary>Декодирует Base32; допускает нижний регистр, пробелы, дефисы и буквы-синонимы O/0, I/L/1.</summary>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new List<byte>((text.Length * 5) / 8 + 1);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char symbol in Normalize(text))
        {
            int value = Alphabet.IndexOf(symbol);
            if (value < 0)
            {
                throw new LicenseFormatException($"Недопустимый символ «{symbol}» в Base32-строке.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. output];
    }

    /// <summary>Разбивает строку на группы по 5 символов для читаемости.</summary>
    public static string Group(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length + (text.Length / 5) + 1);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % 5 == 0)
            {
                builder.Append('-');
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    /// <summary>Убирает группирующие дефисы и пробелы.</summary>
    public static string Ungroup(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        foreach (char symbol in text)
        {
            if (symbol is '-' or ' ' or '\t' or '\r' or '\n')
            {
                continue;
            }

            builder.Append(symbol);
        }

        return builder.ToString();
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char raw in text)
        {
            char symbol = char.ToUpperInvariant(raw);
            switch (symbol)
            {
                case '-' or ' ' or '\t' or '\r' or '\n':
                    continue;
                case 'O':
                    builder.Append('0');
                    break;
                case 'I' or 'L':
                    builder.Append('1');
                    break;
                default:
                    builder.Append(symbol);
                    break;
            }
        }

        return builder.ToString();
    }
}
