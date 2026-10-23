using System.Text.Json.Nodes;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Миграция манифеста проекта с одной версии схемы на следующую (заготовка
/// долговечности: файлы старых версий читаются и после обновления приложения).
/// </summary>
public interface IProjectMigration
{
    /// <summary>Версия схемы, с которой мигрирует этот шаг.</summary>
    int FromVersion { get; }

    /// <summary>Версия схемы после миграции.</summary>
    int ToVersion { get; }

    /// <summary>Преобразовать манифест на месте (JSON-дерево, до десериализации в DTO).</summary>
    void Migrate(JsonObject manifest);
}

/// <summary>
/// Цепочка миграций схемы манифеста до текущей версии.
/// Новая версия схемы = новый класс <see cref="IProjectMigration"/>, зарегистрированный
/// в <see cref="Migrations"/> с шагом от предыдущей версии; чтение файла со старой схемой
/// проходит всю цепочку автоматически. Файлы более новой версии не читаются
/// (данные не угадываются — предлагается обновить приложение).
/// </summary>
public static class ProjectSchemaMigrator
{
    private static readonly IProjectMigration[] Chain = [new ProjectMigrationV0ToV1()];

    /// <summary>Зарегистрированные шаги миграции (в порядке применения).</summary>
    public static IReadOnlyList<IProjectMigration> Migrations => Chain;

    /// <summary>Текущая версия схемы манифеста.</summary>
    public static int CurrentVersion => ProjectFileFormat.CurrentSchemaVersion;

    /// <summary>
    /// Привести манифест к текущей версии схемы; <paramref name="fromVersion"/> —
    /// версия, с которой начиналось обновление (для отчёта о миграции).
    /// </summary>
    public static JsonObject Upgrade(JsonObject manifest, int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        int version = fromVersion;
        while (version < CurrentVersion)
        {
            IProjectMigration? step = Chain.FirstOrDefault(m => m.FromVersion == version);
            if (step is null)
            {
                throw new ProjectFileException(
                    $"Нет миграции схемы проекта с версии {version} до {CurrentVersion} — файл прочитать невозможно.");
            }

            step.Migrate(manifest);
            version = step.ToVersion;
            manifest["schemaVersion"] = version;
        }

        return manifest;
    }
}
