using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// DTO JSON-манифеста проекта (внутреннее представление схемы версии 1).
/// Схема документирована полями DTO; порядок свойств фиксирует порядок полей
/// в JSON (детерминированные, байт-в-байт повторяемые файлы).
/// </summary>
internal sealed class ProjectManifestDto
{
    public required string Format { get; init; }

    public required int SchemaVersion { get; init; }

    /// <summary>Метаданные; отсутствие допустимо (заполняются значениями по умолчанию).</summary>
    public ProjectMetadata Metadata { get; init; } = ProjectMetadata.CreateDefault();

    /// <summary>Явные единицы длины проекта (без них масштаб недопустимо неоднозначен).</summary>
    public required LengthUnit Units { get; init; }

    /// <summary>Настройки вида (камера/проекция); отсутствие — вид по умолчанию.</summary>
    public ProjectViewSettings View { get; init; } = ProjectViewSettings.Default;

    public List<ProjectMaterial>? Materials { get; init; }

    /// <summary>Корень сцены: узел с детьми (id корня диагностический, в модель не переносится).</summary>
    public required ProjectNodeDto Scene { get; init; }

    /// <summary>Определения компонентов: один definition — много instance.</summary>
    public List<ProjectComponentDto>? Components { get; init; }

    /// <summary>Таблица ассетов (бинарные меши).</summary>
    public List<ProjectAssetDto>? Assets { get; init; }
}

/// <summary>Узел сцены в манифесте: группа, меш или инстанс компонента.</summary>
internal sealed class ProjectNodeDto
{
    /// <summary>Идентификатор узла (сохраняется при round-trip; у корня — диагностический).</summary>
    public Guid Id { get; init; }

    /// <summary>Тип узла: см. <see cref="ProjectNodeTypes"/>.</summary>
    public required string Type { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Видимость узла; отсутствие поля — узел видим.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>Локальная трансформация; отсутствие поля — единичная матрица.</summary>
    public Transform3d Transform { get; init; } = Transform3d.Identity;

    /// <summary>Идентификатор ассета меша (тип <see cref="ProjectNodeTypes.Mesh"/>).</summary>
    public string? Mesh { get; init; }

    /// <summary>Идентификатор определения компонента (тип <see cref="ProjectNodeTypes.ComponentInstance"/>).</summary>
    public Guid? Definition { get; init; }

    public List<ProjectNodeDto>? Children { get; init; }
}

/// <summary>Определение компонента: мастер-контент, который инстансируют узлы сцены.</summary>
internal sealed class ProjectComponentDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public required ProjectNodeDto Content { get; init; }
}

/// <summary>Запись таблицы ассетов: бинарный меш с контрольной суммой.</summary>
internal sealed class ProjectAssetDto
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>Путь записи внутри контейнера (assets/meshes/&lt;id&gt;.bin).</summary>
    public required string Path { get; init; }

    public long ByteLength { get; init; }

    /// <summary>SHA-256 содержимого в нижнем hex-регистре.</summary>
    public required string Sha256 { get; init; }
}

/// <summary>Типы узлов сцены в манифесте.</summary>
internal static class ProjectNodeTypes
{
    public const string Group = "group";
    public const string Mesh = "mesh";
    public const string ComponentInstance = "componentInstance";
}

/// <summary>Общие настройки сериализации манифеста.</summary>
internal static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
