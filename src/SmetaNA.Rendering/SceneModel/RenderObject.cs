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
        BoundingBox3d bounds,
        float[]? triangleColors = null)
    {
        ArgumentNullException.ThrowIfNull(selectionNode);
        ArgumentNullException.ThrowIfNull(sourceNode);
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(edgeIndices);
        if (edgeIndices.Length % 2 != 0)
        {
            throw new ArgumentException("Массив рёбер должен содержать пары индексов.", nameof(edgeIndices));
        }

        if (triangleColors is not null && triangleColors.Length != (triangles.Indices.Count / 3) * 4)
        {
            throw new ArgumentException(
                "Цвета треугольников задаются по 4 канала RGBA на треугольник.",
                nameof(triangleColors));
        }

        SelectionNode = selectionNode;
        SourceNode = sourceNode;
        Triangles = triangles;
        EdgeIndices = edgeIndices;
        Bounds = bounds;
        TriangleColors = triangleColors;
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

    /// <summary>
    /// Путь цвета материалов: RGBA в долях 0..1 по 4 канала на треугольник
    /// (в порядке <see cref="MeshData.Indices"/>); <c>null</c> — цвета материалов
    /// не назначены, рисуется цвет по умолчанию.
    /// </summary>
    public float[]? TriangleColors { get; }

    /// <summary>Есть ли пообъектные цвета материалов (<see cref="TriangleColors"/>).</summary>
    public bool HasTriangleColors => TriangleColors is not null;

    public int TriangleCount => Triangles.Indices.Count / 3;

    public int EdgeCount => EdgeIndices.Length / 2;
}
