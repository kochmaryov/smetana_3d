using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Метаданные проекта: заголовок, отметки времени и генератор файла.
/// Значения пишутся в манифест как есть и не влияют на геометрию.
/// </summary>
public sealed record ProjectMetadata
{
    /// <summary>Заголовок проекта (имя без расширения), может отсутствовать.</summary>
    public string? Title { get; init; }

    /// <summary>Момент создания проекта (UTC).</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Момент последнего изменения проекта (UTC); обновляет вызывающая сторона.</summary>
    public DateTimeOffset ModifiedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Генератор файла (приложение и версия) — для диагностики старых файлов.</summary>
    public string Generator { get; init; } = ProjectDocument.DefaultGenerator;

    /// <summary>Метаданные по умолчанию с текущими отметками времени.</summary>
    public static ProjectMetadata CreateDefault() => new();
}

/// <summary>
/// Простой материал проекта: имя и цвет RGBA (каналы 0..1).
/// Назначение материала — через <c>MeshFace.MaterialIndex</c>: индекс в общем
/// списке материалов проекта (<see cref="ProjectDocument.Materials"/>), −1 — без материала.
/// </summary>
public sealed record ProjectMaterial
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public double Red { get; init; } = 0.8;

    public double Green { get; init; } = 0.8;

    public double Blue { get; init; } = 0.8;

    public double Alpha { get; init; } = 1.0;
}

/// <summary>
/// Документ проекта SmetaNA: сцена с явными единицами, настройки вида, материалы
/// и метаданные. Единицы измерения — <see cref="Scene.Unit"/> (один источник истины;
/// свойство <see cref="Unit"/> — удобное зеркало).
/// </summary>
public sealed class ProjectDocument
{
    /// <summary>Имя генератора файлов по умолчанию.</summary>
    public const string DefaultGenerator = "SmetaNA";

    /// <summary>Сцена проекта: иерархия узлов (группы, меши, инстансы компонентов).</summary>
    public Scene Scene { get; init; } = new();

    /// <summary>Единицы длины проекта; пишутся в манифест явно.</summary>
    public LengthUnit Unit
    {
        get => Scene.Unit;
        set => Scene.Unit = value;
    }

    /// <summary>Настройки вида: камера и проекция.</summary>
    public ProjectViewSettings View { get; set; } = ProjectViewSettings.Default;

    /// <summary>Материалы проекта; индексы граней мешей ссылаются на этот список.</summary>
    public List<ProjectMaterial> Materials { get; init; } = [];

    /// <summary>Метаданные проекта (заголовок, отметки времени, генератор).</summary>
    public ProjectMetadata Metadata { get; set; } = ProjectMetadata.CreateDefault();
}
