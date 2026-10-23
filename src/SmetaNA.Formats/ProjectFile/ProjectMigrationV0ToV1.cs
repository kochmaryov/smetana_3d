using System.Text.Json.Nodes;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Миграция черновой схемы 0 (до публичного формата) на схему 1:
/// <c>unit</c> (код «mm»/«cm»/«m»/«in») → <c>units</c> (полное имя enum),
/// <c>camera</c> → <c>view</c> (проекция «ortho» → «orthographic»),
/// <c>kind</c> → <c>type</c> в узлах сцены и компонентов, недостающие таблицы
/// материалов/компонентов/ассетов дополняются пустыми.
/// </summary>
internal sealed class ProjectMigrationV0ToV1 : IProjectMigration
{
    public int FromVersion => 0;

    public int ToVersion => 1;

    public void Migrate(JsonObject manifest)
    {
        if (manifest.Remove("unit", out JsonNode? unitNode) && unitNode is not null)
        {
            manifest["units"] = MapUnit(unitNode.GetValue<string>());
        }

        if (manifest.Remove("camera", out JsonNode? cameraNode) && cameraNode is JsonObject camera)
        {
            if (camera["projection"] is JsonValue projection)
            {
                camera["projection"] = MapProjection(projection.GetValue<string>());
            }

            manifest["view"] = camera;
        }

        if (manifest["scene"] is JsonObject scene)
        {
            RenameNodeKinds(scene);
        }

        if (manifest["components"] is JsonArray components)
        {
            foreach (JsonNode? component in components)
            {
                if (component?["content"] is JsonObject content)
                {
                    RenameNodeKinds(content);
                }
            }
        }

        manifest["materials"] ??= new JsonArray();
        manifest["components"] ??= new JsonArray();
        manifest["assets"] ??= new JsonArray();
    }

    private static void RenameNodeKinds(JsonObject node)
    {
        if (node.Remove("kind", out JsonNode? kind) && kind is not null)
        {
            node["type"] = kind;
        }

        if (node["children"] is JsonArray children)
        {
            foreach (JsonNode? child in children)
            {
                if (child is JsonObject childObject)
                {
                    RenameNodeKinds(childObject);
                }
            }
        }
    }

    private static string MapUnit(string unit) => unit switch
    {
        "mm" => "millimeter",
        "cm" => "centimeter",
        "m" => "meter",
        "in" or "inch" => "inch",
        _ => throw new ProjectFileException($"Неизвестный код единиц «{unit}» в файле со схемой 0."),
    };

    private static string MapProjection(string projection) => projection switch
    {
        "ortho" => "orthographic",
        "perspective" => "perspective",
        _ => throw new ProjectFileException($"Неизвестный режим проекции «{projection}» в файле со схемой 0."),
    };
}
