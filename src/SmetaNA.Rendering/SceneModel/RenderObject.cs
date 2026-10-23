using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Rendering;

/// <summary>
/// Объект рендера/пиккинга: мировые треугольники узла меша, его рёбра
/// и целевой узел выделения (сам меш или содержащая группа/компонент).
/// </summary>
public sealed class RenderObject
{
    public RenderObject(
        SceneNode selectionNode,
        SceneNode sourceNode,
        MeshData triangles,
        int[] edgeIndices,
        BoundingBox3d bounds)
    {
        ArgumentNullException.ThrowIfNull(selectionNode);
        ArgumentNullException.ThrowIfNull(sourceNode);
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(edgeIndices);
        if (edgeIndices.Length % 2 != 0)
        {
            throw new ArgumentException("Массив рёбер должен содержать пары индексов.", nameof(edgeIndices));
        }

        SelectionNode = selectionNode;
        SourceNode = sourceNode;
        Triangles = triangles;
        EdgeIndices = edgeIndices;
        Bounds = bounds;
    }

    /// <summary>Узел, который выделяется при попадании (объект/группа/компонент).</summary>
    public SceneNode SelectionNode { get; }

    /// <summary>Узел с геометрией (MeshNode или узел внутри определения компонента).</summary>
    public SceneNode SourceNode { get; }

    /// <summary>Мировые треугольники (мм).</summary>
    public MeshData Triangles { get; }

    /// <summary>Пары индексов рёбер в <see cref="MeshData.Positions"/> (без дублей).</summary>
    public int[] EdgeIndices { get; }

    public BoundingBox3d Bounds { get; }

    public int TriangleCount => Triangles.Indices.Count / 3;

    public int EdgeCount => EdgeIndices.Length / 2;
}
