using SmetaNA.Core.Model;

namespace SmetaNA.Formats;

/// <summary>Опции экспорта меша.</summary>
/// <param name="Unit">
/// Единицы чисел в файле: координаты (мм) делятся на масштаб единицы.
/// По умолчанию миллиметры — согласовано с базой редактора.
/// </param>
/// <param name="ModelName">Имя модели для текстовых заголовков (solid/o).</param>
public sealed record MeshExportOptions(LengthUnit Unit = LengthUnit.Millimeter, string ModelName = "SmetaNA")
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
