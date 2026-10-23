namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Константы формата проекта SmetaNA (модуль M9). Контейнер — ZIP-архив:
/// <see cref="ManifestEntryName"/> (JSON-манифест со схемой, единицами и видом)
/// и бинарные ассеты мешей в <see cref="MeshAssetsDirectory"/> (double-позиции, не текст).
/// <para>
/// Принципы долговечности (спека: «данные пользователя переживают приложение»):
/// открытая JSON-схема без вендорских зависимостей; явная версия схемы и цепочка миграций
/// (<see cref="ProjectSchemaMigrator"/>); неизвестные поля при чтении игнорируются (файлы
/// более новых минорных версий); контрольные суммы ассетов ловят повреждения;
/// double-значения пишутся и читаются бит-в-бит.
/// </para>
/// </summary>
public static class ProjectFileFormat
{
    /// <summary>Расширение файла проекта SmetaNA.</summary>
    public const string Extension = ".sna";

    /// <summary>Идентификатор формата в манифесте: защита от чужих ZIP-контейнеров.</summary>
    public const string FormatId = "SmetaNA.Project";

    /// <summary>Текущая версия схемы манифеста.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Имя записи JSON-манифеста в контейнере.</summary>
    public const string ManifestEntryName = "project.json";

    /// <summary>Каталог бинарных ассетов мешей внутри контейнера.</summary>
    public const string MeshAssetsDirectory = "assets/meshes/";

    /// <summary>Вид ассета «полумногоугольный меш» в таблице ассетов манифеста.</summary>
    public const string MeshAssetKind = "halfEdgeMesh";
}
