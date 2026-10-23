namespace SmetaNA.Core.Model;

/// <summary>
/// Таблица тегов (слоёв) проекта: создание, переименование с проверкой
/// уникальности имени, поиск и удаление. Живёт в <see cref="Scene.Tags"/>.
/// </summary>
public sealed class TagTable
{
    private readonly List<Tag> _tags = [];

    public IReadOnlyList<Tag> Tags => _tags;

    /// <summary>Создание тега с уникальным именем.</summary>
    public Tag Create(string name)
    {
        ValidateName(name);
        var tag = new Tag { Name = name };
        Add(tag);
        return tag;
    }

    /// <summary>Добавление готового тега (команды undo, загрузка проекта).</summary>
    public void Add(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ValidateName(tag.Name);
        if (_tags.Contains(tag))
        {
            throw new InvalidOperationException($"Тег «{tag.Name}» уже добавлен в таблицу.");
        }

        if (FindByName(tag.Name) is not null)
        {
            throw new InvalidOperationException($"Тег с именем «{tag.Name}» уже существует.");
        }

        _tags.Add(tag);
    }

    /// <summary>Удаление тега из таблицы; привязки узлов снимают команды.</summary>
    public void Remove(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (!_tags.Remove(tag))
        {
            throw new InvalidOperationException($"Тег «{tag.Name}» не найден в таблице.");
        }
    }

    /// <summary>Переименование тега с проверкой уникальности имени.</summary>
    public void Rename(Tag tag, string name)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ValidateName(name);
        if (!_tags.Contains(tag))
        {
            throw new InvalidOperationException($"Тег «{tag.Name}» не найден в таблице.");
        }

        Tag? conflict = FindByName(name);
        if (conflict is not null && !ReferenceEquals(conflict, tag))
        {
            throw new InvalidOperationException($"Тег с именем «{name}» уже существует.");
        }

        tag.Name = name;
    }

    /// <summary>Поиск по имени без учёта регистра.</summary>
    public Tag? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _tags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public Tag? FindById(Guid id) => _tags.FirstOrDefault(t => t.Id == id);

    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Имя тега не может быть пустым.", nameof(name));
        }
    }
}
