using System.Text.Json;

namespace SmetaNA.Licensing;

/// <summary>Единые настройки JSON для подписанных полезных нагрузок и файлов-конвертов.</summary>
internal static class LicenseJson
{
    /// <summary>Каноничная сериализация подписываемых данных (компактная, без отступов).</summary>
    public static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>Сериализация файлов-конвертов (читаемая, с отступами).</summary>
    public static readonly JsonSerializerOptions Envelope = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
