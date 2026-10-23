using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools;

/// <summary>Ссылка на грань меша: узел сцены и грань его меша.</summary>
public readonly record struct MeshFaceRef(MeshNode Node, MeshFace Face);

/// <summary>
/// Выборка инструмента: объект сцены и текущая грань (для граневых операций
/// вроде Push/Pull). Изменения публикуются событием <see cref="Changed"/>.
/// </summary>
public sealed class ToolSelection
{
    /// <summary>Выбранный объект (узел) или <c>null</c>.</summary>
    public SceneNode? Node { get; private set; }

    /// <summary>Выбранная грань или <c>null</c>.</summary>
    public MeshFaceRef? Face { get; private set; }

    public event Action? Changed;

    public void SetNode(SceneNode? node)
    {
        if (ReferenceEquals(Node, node) && Face is null)
        {
            return;
        }

        Node = node;
        Face = null;
        Changed?.Invoke();
    }

    public void SetFace(MeshFaceRef? face)
    {
        if (Face == face)
        {
            return;
        }

        Face = face;
        Node = face?.Node;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Node is null && Face is null)
        {
            return;
        }

        Node = null;
        Face = null;
        Changed?.Invoke();
    }
}
