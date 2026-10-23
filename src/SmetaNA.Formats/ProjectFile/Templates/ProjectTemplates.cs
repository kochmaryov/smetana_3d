using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile.Templates;

/// <summary>
/// Встроенные шаблоны проектов (модуль M9): единицы и стартовые сцены —
/// «пусто в мм», пример «стакан», пример «подставка».
/// </summary>
public static class ProjectTemplates
{
    /// <summary>Идентификатор шаблона «пустой проект в миллиметрах».</summary>
    public const string EmptyMillimeterId = "empty-millimeter";

    /// <summary>Идентификатор шаблона-примера «стакан».</summary>
    public const string CupId = "cup";

    /// <summary>Идентификатор шаблона-примера «подставка».</summary>
    public const string StandId = "stand";

    private static readonly ProjectTemplate[] Catalog =
    [
        new ProjectTemplate(
            EmptyMillimeterId,
            "Пусто (мм)",
            "Пустая сцена в миллиметрах, изометрический вид.",
            LengthUnit.Millimeter,
            CreateEmptyMillimeter),
        new ProjectTemplate(
            CupId,
            "Стакан — пример",
            "Стакан 60×60×80 мм со стенкой 5 мм: базовая геометрия для Push/Pull и 3D-печати.",
            LengthUnit.Millimeter,
            CreateCup),
        new ProjectTemplate(
            StandId,
            "Подставка — пример",
            "Подставка-ступенька 100×60×50 мм: групповая структура и точные размеры.",
            LengthUnit.Millimeter,
            CreateStand),
    ];

    /// <summary>Все встроенные шаблоны в порядке показа.</summary>
    public static IReadOnlyList<ProjectTemplate> All => Catalog;

    /// <summary>Найти шаблон по идентификатору; неизвестный идентификатор — null.</summary>
    public static ProjectTemplate? Find(string id) =>
        Catalog.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    /// <summary>Создать документ по идентификатору шаблона.</summary>
    public static ProjectDocument CreateDocument(string id) =>
        Find(id)?.CreateDocument()
        ?? throw new ArgumentException($"Неизвестный шаблон проекта «{id}».", nameof(id));

    private static ProjectDocument CreateEmptyMillimeter() => new()
    {
        Unit = LengthUnit.Millimeter,
        View = ProjectViewSettings.Default,
        Metadata = ProjectMetadata.CreateDefault() with { Title = "Новый проект" },
    };

    private static ProjectDocument CreateCup()
    {
        var document = new ProjectDocument
        {
            Unit = LengthUnit.Millimeter,
            Metadata = ProjectMetadata.CreateDefault() with { Title = "Стакан" },
        };
        document.Scene.Root.AddChild(new MeshNode
        {
            Name = "Стакан",
            Mesh = TemplateMeshes.Cup(),
        });
        document.View = ProjectViewSettings.Default with
        {
            Target = new Vector3d(30, 30, 40),
            Distance = 300,
        };
        return document;
    }

    private static ProjectDocument CreateStand()
    {
        var document = new ProjectDocument
        {
            Unit = LengthUnit.Millimeter,
            Metadata = ProjectMetadata.CreateDefault() with { Title = "Подставка" },
        };
        document.Scene.Root.AddChild(new MeshNode
        {
            Name = "Подставка",
            Mesh = TemplateMeshes.Stand(),
        });
        document.View = ProjectViewSettings.Default with
        {
            Target = new Vector3d(50, 30, 25),
            Distance = 400,
        };
        return document;
    }
}
