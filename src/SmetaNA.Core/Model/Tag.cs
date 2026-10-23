namespace SmetaNA.Core.Model;

/// <summary>
/// Тег (слой) сцены: именованная группа видимости (задача 3.5).
/// Скрытый тег скрывает все узлы, привязанные к нему, вместе с их поддеревьями.
/// </summary>
public sealed class Tag
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Имя тега; уникальность в пределах <see cref="TagTable"/> проверяет таблица.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Видимость тега: false скрывает все узлы с этим тегом.</summary>
    public bool IsVisible { get; set; } = true;
}
