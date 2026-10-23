using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>
/// Файл-конверт «подписанный JSON»: каноничная полезная нагрузка в Base64Url и подпись ECDSA рядом.
/// Подпись покрывает только байты полезной нагрузки, поэтому формат конверта можно менять без инвалидации подписей.
/// </summary>
internal static class SignedEnvelope
{
    private sealed class EnvelopeDocument
    {
        public string Format { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    /// <summary>Собирает текст конверта из полезной нагрузки и подписи.</summary>
    public static string Write(string format, byte[] payload, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signature);

        var document = new EnvelopeDocument
        {
            Format = format,
            Payload = ToBase64Url(payload),
            Signature = ToBase64Url(signature),
        };

        return JsonSerializer.Serialize(document, LicenseJson.Envelope);
    }

    /// <summary>Разбирает конверт и возвращает полезную нагрузку и подпись; формат должен совпадать с ожидаемым.</summary>
    public static (byte[] Payload, byte[] Signature) Read(string envelopeText, string expectedFormat)
    {
        ArgumentNullException.ThrowIfNull(envelopeText);
        ArgumentNullException.ThrowIfNull(expectedFormat);

        EnvelopeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<EnvelopeDocument>(envelopeText, LicenseJson.Envelope);
        }
        catch (JsonException ex)
        {
            throw new LicenseFormatException("Подписанный файл повреждён: некорректный JSON.", ex);
        }

        if (document is null || !string.Equals(document.Format, expectedFormat, StringComparison.Ordinal))
        {
            throw new LicenseFormatException("Подписанный файл повреждён: неожиданный формат.");
        }

        return (FromBase64Url(document.Payload), FromBase64Url(document.Signature));
    }

    private static string ToBase64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] FromBase64Url(string text)
    {
        try
        {
            string base64 = text.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new LicenseFormatException("Подписанный файл повреждён: некорректное кодирование данных.", ex);
        }
    }
}
