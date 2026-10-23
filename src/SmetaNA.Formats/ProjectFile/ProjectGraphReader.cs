using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Восстановление графа сцены из манифеста: определения компонентов создаются все
/// до построения узлов (инстансы ссылаются на общие definition), меши берутся из
/// загруженных ассетов по id (общий ассет — общий экземпляр меша, инстансинг сохраняется).
/// </summary>
internal static class ProjectGraphReader
{
    public static ProjectDocument Build(ProjectManifestDto manifest, IReadOnlyDictionary<string, HalfEdgeMesh> meshes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(meshes);

        EnsureFinite(manifest.View);

        var definitions = new Dictionary<Guid, ComponentDefinition>();
        foreach (ProjectComponentDto component in manifest.Components ?? [])
        {
            var definition = new ComponentDefinition
            {
                Id = component.Id,
                Name = component.Name,
            };
            if (!definitions.TryAdd(component.Id, definition))
            {
                throw new ProjectFileException($"Дублирующееся определение компонента с id {component.Id} — файл повреждён.");
            }
        }

        foreach (ProjectComponentDto component in manifest.Components ?? [])
        {
            definitions[component.Id].Content = ReadNode(component.Content, definitions, meshes);
        }

        var document = new ProjectDocument
        {
            Metadata = manifest.Metadata,
            View = manifest.View,
        };
        document.Scene.Unit = manifest.Units;
        if (manifest.Materials is not null)
        {
            document.Materials.AddRange(manifest.Materials);
        }

        ProjectNodeDto root = manifest.Scene;
        Scene rootNode = document.Scene;
        rootNode.Root.Name = root.Name;
        rootNode.Root.IsVisible = root.Visible;
        rootNode.Root.LocalTransform = ReadTransform(root.Transform);
        if (root.Children is not null)
        {
            foreach (ProjectNodeDto child in root.Children)
            {
                rootNode.Root.AddChild(ReadNode(child, definitions, meshes));
            }
        }

        return document;
    }

    private static SceneNode ReadNode(
        ProjectNodeDto dto,
        IReadOnlyDictionary<Guid, ComponentDefinition> definitions,
        IReadOnlyDictionary<string, HalfEdgeMesh> meshes)
    {
        Transform3d transform = ReadTransform(dto.Transform);
        SceneNode node = dto.Type switch
        {
            ProjectNodeTypes.Group => new GroupNode
            {
                Id = dto.Id,
                Name = dto.Name,
                IsVisible = dto.Visible,
                LocalTransform = transform,
            },
            ProjectNodeTypes.Mesh => new MeshNode
            {
                Id = dto.Id,
                Name = dto.Name,
                IsVisible = dto.Visible,
                LocalTransform = transform,
                Mesh = ResolveMesh(dto, meshes),
            },
            ProjectNodeTypes.ComponentInstance => new ComponentInstanceNode
            {
                Id = dto.Id,
                Name = dto.Name,
                IsVisible = dto.Visible,
                LocalTransform = transform,
                Definition = ResolveDefinition(dto, definitions),
            },
            _ => throw new ProjectFileException(
                $"Неизвестный тип узла «{dto.Type}» в файле проекта — файл создан более новой версией или повреждён."),
        };

        if (dto.Children is not null)
        {
            foreach (ProjectNodeDto child in dto.Children)
            {
                node.AddChild(ReadNode(child, definitions, meshes));
            }
        }

        return node;
    }

    private static HalfEdgeMesh ResolveMesh(
        ProjectNodeDto dto,
        IReadOnlyDictionary<string, HalfEdgeMesh> meshes)
    {
        string assetId = dto.Mesh
            ?? throw new ProjectFileException("Узел-меш не ссылается на ассет геометрии — файл повреждён.");
        return meshes.TryGetValue(assetId, out HalfEdgeMesh? mesh)
            ? mesh
            : throw new ProjectFileException($"Ассет меша «{assetId}» отсутствует в файле проекта — файл повреждён.");
    }

    private static ComponentDefinition ResolveDefinition(
        ProjectNodeDto dto,
        IReadOnlyDictionary<Guid, ComponentDefinition> definitions)
    {
        Guid definitionId = dto.Definition
            ?? throw new ProjectFileException("Инстанс компонента не ссылается на определение — файл повреждён.");
        return definitions.TryGetValue(definitionId, out ComponentDefinition? definition)
            ? definition
            : throw new ProjectFileException($"Определение компонента {definitionId} отсутствует в файле проекта — файл повреждён.");
    }

    private static Transform3d ReadTransform(Transform3d transform)
    {
        double[] values =
        [
            transform.M11, transform.M12, transform.M13, transform.M14,
            transform.M21, transform.M22, transform.M23, transform.M24,
            transform.M31, transform.M32, transform.M33, transform.M34,
            transform.M41, transform.M42, transform.M43, transform.M44,
        ];
        if (values.Any(v => !double.IsFinite(v)))
        {
            throw new ProjectFileException("Трансформация узла содержит неконечные значения — файл повреждён.");
        }

        return transform;
    }

    private static void EnsureFinite(ProjectViewSettings view)
    {
        double[] values =
        [
            view.Target.X, view.Target.Y, view.Target.Z,
            view.Distance, view.Yaw, view.Pitch,
            view.FieldOfViewY, view.OrthoHeight, view.AspectRatio,
            view.NearPlane, view.FarPlane,
        ];
        if (values.Any(v => !double.IsFinite(v)))
        {
            throw new ProjectFileException("Настройки вида содержат неконечные значения — файл повреждён.");
        }
    }
}
