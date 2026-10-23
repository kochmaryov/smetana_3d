using System.Globalization;

namespace SmetaNA.Core.Materials;

/// <summary>
/// Цвет RGBA (каналы 0..255) для материалов, палитры и путей цвета в рендере.
/// </summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly RgbaColor White = new(255, 255, 255);
    public static readonly RgbaColor Black = new(0, 0, 0);

    /// <summary>Цвет из hex-строки: «#RRGGBB», «RRGGBB» или «#RRGGBBAA».</summary>
    public static RgbaColor FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        string digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (digits.Length != 6 && digits.Length != 8)
        {
            throw new ArgumentException("Hex-цвет должен содержать 6 или 8 hex-цифр.", nameof(hex));
        }

        byte ParseByte(int offset) => byte.Parse(digits.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        byte alpha = digits.Length == 8 ? ParseByte(6) : (byte)255;
        return new RgbaColor(ParseByte(0), ParseByte(2), ParseByte(4), alpha);
    }

    /// <summary>Тот же цвет с другой непрозрачностью (альфа-каналом).</summary>
    public RgbaColor WithAlpha(byte alpha) => this with { A = alpha };

    /// <summary>Hex-представление: «#RRGGBB» или «#RRGGBBAA» при непрозрачности меньше 1.</summary>
    public string ToHex() => A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}{A:X2}");

    /// <summary>Каналы в долях 0..1 (путь цвета в рендере).</summary>
    public (float R, float G, float B, float A) ToNormalized() => (R / 255f, G / 255f, B / 255f, A / 255f);

    public override string ToString() => ToHex();
}
