using SmetaNA.Core.Model;

namespace SmetaNA.Formats;

/// <summary>Опции экспорта меша.</summary>
/// <param name="Unit">
/// Единицы чисел в файле: координаты (мм) делятся на масштаб единицы.
/// По умолчанию миллиметры — согласовано с базой редактора.
/// </param>
/// <param name="ModelName">Имя модели для текстовых заголовков (solid/o) и метаданных 3MF.</param>
/// <param name="Metadata">
/// Метаданные модели для форматов, которые их хранят (3MF: блок metadata);
/// ключи Title и Application замещают значения по умолчанию. STL/OBJ игнорируют.
/// </param>
public sealed record MeshExportOptions(
    LengthUnit Unit = LengthUnit.Millimeter,
    string ModelName = "SmetaNA",
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>Имя модели без управляющих символов; пустое заменяется на «SmetaNA».</summary>
    public string SafeModelName
    {
        get
        {
            string name = (ModelName ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return name.Length == 0 ? "SmetaNA" : name;
        }
    }
}
