using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Tools.Editing;

/// <summary>
/// Строки модуля инструментов редактирования (задача 2.3): локализация в своём
/// файле-модуле, общие resx не изменяются. Координатор может перенести ключи
/// в общий каталог после подключения модуля.
/// </summary>
public static class EditingToolStrings
{
    /// <summary>Ключи строк модуля (совпадают с NameKey регистраций).</summary>
    public static class Keys
    {
        public const string Offset = "Editing.Tool.Offset";
        public const string Move = "Editing.Tool.Move";
        public const string Rotate = "Editing.Tool.Rotate";
        public const string Scale = "Editing.Tool.Scale";
        public const string Eraser = "Editing.Tool.Eraser";
        public const string Soften = "Editing.Tool.Soften";
        public const string Paint = "Editing.Tool.Paint";

        /// <summary>Все ключи модуля.</summary>
        public static IReadOnlyList<string> All { get; } =
        [
            Offset, Move, Rotate, Scale, Eraser, Soften, Paint,
        ];
    }

    private static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>
    {
        [Keys.Offset] = "Смещение контура",
        [Keys.Move] = "Перемещение",
        [Keys.Rotate] = "Вращение",
        [Keys.Scale] = "Масштаб",
        [Keys.Eraser] = "Ластик",
        [Keys.Soften] = "Сглаживание",
        [Keys.Paint] = "Заливка",
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        [Keys.Offset] = "Offset",
        [Keys.Move] = "Move",
        [Keys.Rotate] = "Rotate",
        [Keys.Scale] = "Scale",
        [Keys.Eraser] = "Eraser",
        [Keys.Soften] = "Soften/Smooth",
        [Keys.Paint] = "Paint Bucket",
    };

    /// <summary>Строка по ключу и культуре; неизвестный ключ возвращается как есть.</summary>
    public static string Get(string key, EditorCulture culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        IReadOnlyDictionary<string, string> table = culture == EditorCulture.English ? English : Russian;
        return table.TryGetValue(key, out string? text) ? text : key;
    }
}
