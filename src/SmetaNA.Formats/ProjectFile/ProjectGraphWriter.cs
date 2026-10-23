using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Formats.ProjectFile;

/// <summary>
/// Захват графа сцены в манифест: уникальные меши и определения компонентов
/// регистрируются по ссылкам (один definition / один меш — один ассет, много инстансов),
/// id ассетов детерминированы («m0», «m1», …) — повторное сохранение даёт байт-в-байт
/// тот же файл.
/// </summary>
internal sealed class ProjectGraphWriter
{
    private readonly Dictionary<HalfEdgeMesh, string> _meshIds = new(ReferenceEqualityComparer.Instance);
    private readonly List<ProjectAssetRecord> _assets = [];
    private readonly List<ComponentDefinition> _definitionOrder = [];
    private readonly Dictionary<ComponentDefinition, int> _definitions = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Guid> _definitionIds = [];

    /// <summary>Кодированные ассеты мешей в порядке регистрации.</summary>
    public IReadOnlyList<ProjectAssetRecord> Assets => _assets;

    public ProjectManifestDto Capture(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        RegisterNode(document.Scene.Root);

        List<ProjectComponentDto> components = [];
        for (int i = 0; i < _definitionOrder.Count; i++)
        {
            ComponentDefinition definition = _definitionOrder[i];
            components.Add(new ProjectComponentDto
            {
                Id = definition.Id,
                Name = definition.Name,
                Content = WriteNode(definition.Content),
            });
        }

        return new ProjectManifestDto
        {
            Format = ProjectFileFormat.FormatId,
            SchemaVersion = ProjectFileFormat.CurrentSchemaVersion,
            Metadata = document.Metadata,
            Units = document.Scene.Unit,
            View = document.View,
            Materials = document.Materials.Count > 0 ? [.. document.Materials] : null,
            Scene = WriteNode(document.Scene.Root),
            Components = components.Count > 0 ? components : null,
            Assets = _assets.Count > 0
                ? [.. _assets.Select(a => new ProjectAssetDto
                {
                    Id = a.Id,
                    Kind = a.Kind,
                    Path = a.Path,
                    ByteLength = a.Bytes.Length,
                    Sha256 = a.Sha256,
                })]
                : null,
        };
    }

    private void RegisterNode(SceneNode node)
    {
        switch (node)
        {
            case MeshNode meshNode:
                RegisterMesh(meshNode.Mesh);
                break;
            case ComponentInstanceNode instanceNode:
                RegisterDefinition(instanceNode.Definition);
                break;
        }

        foreach (SceneNode child in node.Children)
        {
            RegisterNode(child);
        }
    }

    private void RegisterMesh(HalfEdgeMesh mesh)
    {
        if (_meshIds.ContainsKey(mesh))
        {
            return;
        }

        string id = $"m{_assets.Count}";
        byte[] bytes = MeshAssetCodec.Encode(mesh);
        _meshIds[mesh] = id;
        _assets.Add(new ProjectAssetRecord(
            id,
            ProjectFileFormat.MeshAssetsDirectory + id + ".bin",
            bytes,
            MeshAssetCodec.ComputeHash(bytes)));
    }

    private void RegisterDefinition(ComponentDefinition definition)
    {
        if (_definitions.ContainsKey(definition))
        {
            return;
        }

        if (!_definitionIds.Add(definition.Id))
        {
            throw new ProjectFileException(
                $"Два разных определения компонентов имеют одинаковый id {definition.Id} — файл был бы неоднозначным.");
        }

        _definitions[definition] = _definitionOrder.Count;
        _definitionOrder.Add(definition);
        RegisterNode(definition.Content);
    }

    private ProjectNodeDto WriteNode(SceneNode node)
    {
        List<ProjectNodeDto>? children = node.Children.Count > 0
            ? [.. node.Children.Select(WriteNode)]
            : null;

        return node switch
        {
            MeshNode meshNode => new ProjectNodeDto
            {
                Id = node.Id,
                Type = ProjectNodeTypes.Mesh,
                Name = node.Name,
                Visible = node.IsVisible,
                Transform = node.LocalTransform,
                Mesh = _meshIds[meshNode.Mesh],
                Children = children,
            },
            ComponentInstanceNode instanceNode => new ProjectNodeDto
            {
                Id = node.Id,
                Type = ProjectNodeTypes.ComponentInstance,
                Name = node.Name,
                Visible = node.IsVisible,
                Transform = node.LocalTransform,
                Definition = instanceNode.Definition.Id,
                Children = children,
            },
            GroupNode => new ProjectNodeDto
            {
                Id = node.Id,
                Type = ProjectNodeTypes.Group,
                Name = node.Name,
                Visible = node.IsVisible,
                Transform = node.LocalTransform,
                Children = children,
            },
            _ => throw new ProjectFileException($"Неизвестный тип узла сцены: {node.GetType().Name}."),
        };
    }
}

/// <summary>Кодированный бинарный ассет меша, готовый к записи в контейнер.</summary>
internal sealed record ProjectAssetRecord(string Id, string Path, byte[] Bytes, string Sha256)
{
    public string Kind => ProjectFileFormat.MeshAssetKind;
}
