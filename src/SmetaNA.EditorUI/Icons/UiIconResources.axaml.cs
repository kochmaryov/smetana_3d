using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace SmetaNA.EditorUI.Icons;

/// <summary>
/// Ресурсы icon-pack интерфейса (контракт <see cref="UiIconId"/>): path
/// mini-language для каждого глифа, поле 24×24, stroke-стиль без заливки —
/// тот же формат, что и у <see cref="ToolIconResources"/>. Хранение — словарь
/// строк (ключ «UiIcon.&lt;имя&gt;»), отрисовка — контрол <see cref="UiIcon"/>.
/// </summary>
public partial class UiIconResources : ResourceDictionary
{
    private const string KeyPrefix = "UiIcon.";
    private static readonly Dictionary<UiIconId, StreamGeometry> GeometryCache = [];
    private static readonly Lazy<UiIconResources> LazyInstance = new(static () => new UiIconResources());

    public UiIconResources() => AvaloniaXamlLoader.Load(this);

    /// <summary>Общий экземпляр словаря (поиск глифов для отрисовки).</summary>
    public static UiIconResources Instance => LazyInstance.Value;

    /// <summary>Ключ ресурса глифа в словаре.</summary>
    public static string Key(UiIconId iconId) =>
        string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{iconId}");

    /// <summary>
    /// Данные глифа (path mini-language, поле 24×24). <c>false</c> — глифа нет
    /// (<see cref="UiIconId.None"/> или неизвестный идентификатор).
    /// </summary>
    public static bool TryGetPathData(UiIconId iconId, out string pathData)
    {
        if (iconId != UiIconId.None && Instance.TryGetValue(Key(iconId), out var value) && value is string text)
        {
            pathData = text;
            return true;
        }

        pathData = string.Empty;
        return false;
    }

    /// <summary>
    /// Геометрия глифа для отрисовки (кэшируется); <c>null</c> — для
    /// <see cref="UiIconId.None"/>. Парсинг требует инициализированной платформы
    /// Avalonia — вызывать на UI-потоке приложения.
    /// </summary>
    public static StreamGeometry? GetGeometry(UiIconId iconId)
    {
        if (iconId == UiIconId.None)
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
