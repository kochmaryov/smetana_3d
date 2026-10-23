using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Копирование и зеркалирование поддеревьев сцены для модуля M6 (массивы,
/// зеркало/симметрия).
/// <para>
/// Копии (<see cref="DeepCopy"/>) — «копии узлов»: новый меш для каждого
/// MeshNode (включая проволочные рёбра и флаги сглаживания), инстансы
/// компонентов копируются «как инстансы» — ссылаются на то же определение,
/// поэтому правка мастера отражается и на элементах массива. Зеркальные копии
/// (<see cref="DeepMirror"/>) перестраивают геометрию (отражение меняет
/// ориентацию обхода граней — кольца реверсируются, нормали остаются
/// наружными); инстанс получает отдельное «зеркальное» определение, так как
/// отражение не является движением и не может жить в общей трансформации
/// инстанса.
/// </para>
/// </summary>
public static class M6NodeCloner
{
    /// <summary>Глубокая копия поддерева: новые узлы и меши, прежние определения компонентов.</summary>
    public static SceneNode DeepCopy(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return CopyNode(node);
    }

    /// <summary>
    /// Глубокая зеркальная копия поддерева: каждая точка мира <c>w</c>
    /// отображается в <c>w·<paramref name="worldMirror"/></c> (отражение
    /// применяется к мировой точке справа). Трансформации узлов сохраняются —
    /// отражение «зашивается» в геометрию мешей.
    /// </summary>
    public static SceneNode DeepMirror(SceneNode node, Transform3d worldMirror)
    {
        ArgumentNullException.ThrowIfNull(node);
        Transform3d parentWorld = node.Parent is { } parent
            ? M6Transforms.WorldTransformOf(parent)
            : Transform3d.Identity;
        return MirrorNode(node, worldMirror, parentWorld);
    }

    private static SceneNode CopyNode(SceneNode node)
    {
        SceneNode copy = node switch
        {
            MeshNode mesh => new MeshNode
            {
                Mesh = CopyMesh(mesh.Mesh),
                Name = mesh.Name,
                IsVisible = mesh.IsVisible,
                LocalTransform = mesh.LocalTransform,
                Tag = mesh.Tag,
            },
            ComponentInstanceNode instance => new ComponentInstanceNode
            {
                // копия-инстанс ссылается на то же определение (наследование мастера)
                Definition = instance.Definition,
                Name = instance.Name,
                IsVisible = instance.IsVisible,
                LocalTransform = instance.LocalTransform,
                Tag = instance.Tag,
            },
            GroupNode group => new GroupNode
            {
                Name = group.Name,
                IsVisible = group.IsVisible,
                LocalTransform = group.LocalTransform,
                Tag = group.Tag,
            },
            _ => throw new NotSupportedException($"Копирование узлов типа {node.GetType().Name} не поддерживается."),
        };

        foreach (SceneNode child in node.Children)
        {
            copy.AddChild(CopyNode(child));
        }

        return copy;
    }

    private static SceneNode MirrorNode(SceneNode node, Transform3d worldMirror, Transform3d parentWorld)
    {
        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);
        if (!Tools.Editing.EditingTransforms.TryConjugatedMap(world, worldMirror, out Func<Vector3d, Vector3d> map))
        {
            throw new InvalidOperationException("Мировая трансформация узла необратима — зеркало невозможно.");
        }

        SceneNode twin;
        switch (node)
        {
            case MeshNode mesh:
                twin = new MeshNode
                {
                    Mesh = MirroredMesh(mesh.Mesh, map),
                    Name = mesh.Name,
                    IsVisible = mesh.IsVisible,
                    LocalTransform = mesh.LocalTransform,
                    Tag = mesh.Tag,
                };
                break;

            case ComponentInstanceNode instance:
                twin = new ComponentInstanceNode
                {
                    // отражение меняет ориентацию геометрии — инстанс получает
                    // собственное «зеркальное» определение
                    Definition = new ComponentDefinition
                    {
                        Name = instance.Definition.Name,
                        Content = MirrorNode(instance.Definition.Content, worldMirror, world),
                    },
                    Name = instance.Name,
                    IsVisible = instance.IsVisible,
                    LocalTransform = instance.LocalTransform,
                    Tag = instance.Tag,
                };
                break;

            case GroupNode group:
                twin = new GroupNode
                {
                    Name = group.Name,
                    IsVisible = group.IsVisible,
                    LocalTransform = group.LocalTransform,
                    Tag = group.Tag,
                };
                break;

            default:
                throw new NotSupportedException($"Зеркалирование узлов типа {node.GetType().Name} не поддерживается.");
        }

        foreach (SceneNode child in node.Children)
        {
            twin.AddChild(MirrorNode(child, worldMirror, world));
        }

        return twin;
    }

    /// <summary>Копия меша с сохранением проволочных рёбер, материалов и флагов сглаживания.</summary>
    public static HalfEdgeMesh CopyMesh(HalfEdgeMesh source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = new HalfEdgeMesh
        {
            Materials = source.Materials,
            DefaultMaterialIndex = source.DefaultMaterialIndex,
        };

        var mapping = MapVertices(source, copy, static (position, _) => position);
        CopyWireEdges(source, copy, mapping);
        CopyFaces(source, copy, mapping);
        return copy;
    }

    /// <summary>Зеркальная копия меша: позиции отражаются, обход граней реверсируется.</summary>
    public static HalfEdgeMesh MirroredMesh(HalfEdgeMesh source, Func<Vector3d, Vector3d> map)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);
        var copy = new HalfEdgeMesh
        {
            Materials = source.Materials,
            DefaultMaterialIndex = source.DefaultMaterialIndex,
        };

        var mapping = MapVertices(source, copy, (position, _) => map(position));
        CopyWireEdges(source, copy, mapping);
        foreach (MeshFace face in source.Faces)
        {
            IReadOnlyList<MeshHalfEdge> ring = MeshTraversal.EnumerateRing(face).ToArray();
            int count = ring.Count;
            var reversed = new List<MeshVertex>(count) { mapping[ring[0].Origin] };
            for (int i = count - 1; i >= 1; i--)
            {
                reversed.Add(mapping[ring[i].Origin]);
            }

            MeshFace created = copy.AddFace(reversed);
            created.MaterialIndex = face.MaterialIndex;

            // ребро j реверсированного кольца — обратное к ребру (count − 1 − j) исходного
            MeshHalfEdge createdEdge = created.HalfEdge;
            for (int j = 0; j < count; j++)
            {
                MeshHalfEdge sourceEdge = ring[count - 1 - j];
                createdEdge.IsSmooth = sourceEdge.IsSmooth;
                createdEdge.IsSoft = sourceEdge.IsSoft;
                createdEdge = createdEdge.Next!;
            }
        }

        return copy;
    }

    private static Dictionary<MeshVertex, MeshVertex> MapVertices(
        HalfEdgeMesh source,
        HalfEdgeMesh copy,
        Func<Vector3d, int, Vector3d> map)
    {
        var mapping = new Dictionary<MeshVertex, MeshVertex>(source.Vertices.Count);
        for (int i = 0; i < source.Vertices.Count; i++)
        {
            MeshVertex vertex = source.Vertices[i];
            mapping[vertex] = copy.AddVertex(map(vertex.Position, i));
        }

        return mapping;
    }

    private static void CopyWireEdges(
        HalfEdgeMesh source,
        HalfEdgeMesh copy,
        IReadOnlyDictionary<MeshVertex, MeshVertex> mapping)
    {
        var processed = new HashSet<MeshHalfEdge>();
        foreach (MeshHalfEdge edge in source.HalfEdges)
        {
            if (edge.Face is not null || edge.Twin is not { } twin || twin.Face is not null)
            {
                continue;
            }

            if (!processed.Add(edge) || !processed.Add(twin))
            {
                continue;
            }

            (MeshHalfEdge forward, MeshHalfEdge reverse) = copy.AddWireEdge(
                mapping[edge.Origin],
                mapping[twin.Origin]);
            forward.IsSmooth = edge.IsSmooth;
            forward.IsSoft = edge.IsSoft;
            reverse.IsSmooth = twin.IsSmooth;
            reverse.IsSoft = twin.IsSoft;
        }
    }

    private static void CopyFaces(
        HalfEdgeMesh source,
        HalfEdgeMesh copy,
        IReadOnlyDictionary<MeshVertex, MeshVertex> mapping)
    {
        foreach (MeshFace face in source.Faces)
        {
            var ring = new List<MeshVertex>();
            var sourceEdges = new List<MeshHalfEdge>();
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                ring.Add(mapping[edge.Origin]);
                sourceEdges.Add(edge);
            }

            MeshFace created = copy.AddFace(ring);
            created.MaterialIndex = face.MaterialIndex;

            MeshHalfEdge createdEdge = created.HalfEdge;
            foreach (MeshHalfEdge sourceEdge in sourceEdges)
            {
                createdEdge.IsSmooth = sourceEdge.IsSmooth;
                createdEdge.IsSoft = sourceEdge.IsSoft;
                createdEdge = createdEdge.Next!;
            }
        }
    }
}
