using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile.Templates;

/// <summary>
/// Шаблон проекта (модуль M9): явные единицы и стартовая сцена.
/// Каждое создание документа — новая сцена со свежими идентификаторами.
/// </summary>
public sealed class ProjectTemplate
{
    private readonly Func<ProjectDocument> _factory;

    internal ProjectTemplate(string id, string name, string description, LengthUnit unit, Func<ProjectDocument> factory)
    {
        Id = id;
        Name = name;
        Description = description;
        Unit = unit;
        _factory = factory;
    }

    /// <summary>Стабильный идентификатор шаблона (для настроек и ярлыков).</summary>
    public string Id { get; }

    /// <summary>Имя шаблона для интерфейса.</summary>
    public string Name { get; }

    /// <summary>Краткое описание шаблона.</summary>
    public string Description { get; }

    /// <summary>Единицы длины стартового проекта.</summary>
    public LengthUnit Unit { get; }

    /// <summary>Создать новый документ проекта по шаблону.</summary>
    public ProjectDocument CreateDocument() => _factory();
}
