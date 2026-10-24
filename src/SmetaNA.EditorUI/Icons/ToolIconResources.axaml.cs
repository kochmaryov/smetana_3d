using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Ресурсы глифов палитры инструментов (контракт <see cref="ToolIconId"/>, спека
/// tool-palette §4 «Иконки»): path mini-language для каждого глифа, поле 24×24,
/// stroke-стиль без заливки. Хранение — словарь строк (ключ «ToolIcon.&lt;имя&gt;»),
/// отрисовка — контрол <see cref="ToolIcon"/>.
/// </summary>
public partial class ToolIconResources : ResourceDictionary
{
    private const string KeyPrefix = "ToolIcon.";
    private static readonly Dictionary<ToolIconId, StreamGeometry> GeometryCache = [];
    private static readonly Lazy<ToolIconResources> LazyInstance = new(static () => new ToolIconResources());

    public ToolIconResources() => AvaloniaXamlLoader.Load(this);

    /// <summary>Общий экземпляр словаря (поиск глифов для отрисовки).</summary>
    public static ToolIconResources Instance => LazyInstance.Value;

    /// <summary>Ключ ресурса глифа в словаре.</summary>
    public static string Key(ToolIconId iconId) =>
        string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{iconId}");

    /// <summary>
    /// Данные глифа (path mini-language, поле 24×24). <c>false</c> — глифа нет
    /// (<see cref="ToolIconId.None"/> или неизвестный идентификатор).
    /// </summary>
    public static bool TryGetPathData(ToolIconId iconId, out string pathData)
    {
        if (iconId != ToolIconId.None && Instance.TryGetValue(Key(iconId), out var value) && value is string text)
        {
            pathData = text;
            return true;
        }

        pathData = string.Empty;
        return false;
    }

    /// <summary>
    /// Геометрия глифа для отрисовки (кэшируется); <c>null</c> — для
    /// <see cref="ToolIconId.None"/>. Парсинг требует инициализированной платформы
    /// Avalonia — вызывать на UI-потоке приложения.
    /// </summary>
    public static StreamGeometry? GetGeometry(ToolIconId iconId)
    {
        if (iconId == ToolIconId.None)
        {
            return null;
        }

        if (GeometryCache.TryGetValue(iconId, out var cached))
        {
            return cached;
        }

        if (!TryGetPathData(iconId, out var pathData))
        {
            return null;
        }

        StreamGeometry geometry = StreamGeometry.Parse(pathData);
        GeometryCache[iconId] = geometry;
        return geometry;
    }
}
