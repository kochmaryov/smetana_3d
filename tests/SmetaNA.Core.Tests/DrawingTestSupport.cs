using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Поддержка тестов инструментов рисования: контекст с вьюпортом «сверху»,
/// клики в мировых точках, разбор построенной геометрии.
/// Вьюпорт «сверху»: 4 px на мировую единицу на плоскости z = 0
/// (см. <see cref="InferenceTestSupport.TopDown"/>).
/// </summary>
internal static class DrawingTestSupport
{
    /// <summary>Контекст с вьюпортом «сверху» над сценой.</summary>
    public static (ToolContext Context, UndoStack Undo) Setup(Scene scene)
    {
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        context.Viewport = InferenceTestSupport.TopDown();
        return (context, undo);
    }

    /// <summary>Пиксель вьюпорта для мировой точки.</summary>
    public static Vector2d Px(ToolContext context, Vector3d world)
        => InferenceTestSupport.Project(context.Viewport!, world);

    /// <summary>Клик левой кнопкой в мировой точке.</summary>
    public static void Click(ToolContext context, ITool tool, Vector3d world, ToolModifiers modifiers = ToolModifiers.None)
        => tool.OnPointer(PointerInput.Press(Px(context, world), PointerButton.Left, modifiers));

    /// <summary>Движение курсора в мировую точку.</summary>
    public static void MoveTo(ToolContext context, ITool tool, Vector3d world, ToolModifiers modifiers = ToolModifiers.None)
        => tool.OnPointer(PointerInput.Move(Px(context, world), modifiers));

    /// <summary>Клик левой кнопкой в пикселе (для привязок «рядом с точкой»).</summary>
    public static void ClickPx(ToolContext context, ITool tool, Vector2d pixel)
        => tool.OnPointer(PointerInput.Press(pixel, PointerButton.Left));

    /// <summary>Клик правой кнопкой в мировой точке (завершение ломаной).</summary>
    public static void RightClick(ToolContext context, ITool tool, Vector3d world)
        => tool.OnPointer(PointerInput.Press(Px(context, world), PointerButton.Right));

    /// <summary>Перетаскивание штриха: зажатие, движение, отпускание в мировых точках.</summary>
    public static void DragStroke(
        ToolContext context,
        ITool tool,
        IReadOnlyList<Vector3d> points,
        Vector3d? releaseAt = null)
    {
        tool.OnPointer(PointerInput.Press(Px(context, points[0]), PointerButton.Left));
        for (int i = 1; i < points.Count; i++)
        {
            tool.OnPointer(PointerInput.Move(Px(context, points[i])));
        }

        Vector3d release = releaseAt ?? points[^1];
        tool.OnPointer(PointerInput.Release(Px(context, release), PointerButton.Left));
    }

    /// <summary>Нажатие клавиши инструмента.</summary>
    public static void Key(ToolContext context, ITool tool, ToolKey key)
        => tool.OnKey(new KeyInput(key));

    /// <summary>Меш единственного нового меш-узла сцены (рисование «в пустоте»).</summary>
    public static HalfEdgeMesh ShapeMeshOf(Scene scene, int childIndex = -1)
    {
        SceneNode node = childIndex < 0
            ? scene.Root.Children[^1]
            : scene.Root.Children[childIndex];
        return Assert.IsType<MeshNode>(node).Mesh;
    }

    /// <summary>Позиции вершин меша.</summary>
    public static IReadOnlyList<Vector3d> Vertices(HalfEdgeMesh mesh)
        => [.. mesh.Vertices.Select(v => v.Position)];

    /// <summary>Ребра меша как пары точек (по одному на проволочную пару/ребро грани).</summary>
    public static IReadOnlyList<(Vector3d From, Vector3d To)> Edges(HalfEdgeMesh mesh)
    {
        var edges = new List<(Vector3d, Vector3d)>();
        var seen = new HashSet<MeshHalfEdge>();
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            if (!seen.Add(edge) || (edge.Twin is { } twin && !seen.Add(twin)))
            {
                continue;
            }

            edges.Add((edge.Origin.Position, edge.Next!.Origin.Position));
        }

        return edges;
    }

    /// <summary>Совпадение точек с допуском.</summary>
    public static void PointEqual(Vector3d expected, Vector3d actual, double tolerance = 1e-9)
        => Assert.True(
            (expected - actual).Length() <= tolerance,
            $"Ожидалась точка {expected}, получена {actual}.");

    /// <summary>Совпадение наборов точек (без учёта порядка) с допуском.</summary>
    public static void PointsEqual(
        IEnumerable<Vector3d> expected,
        IEnumerable<Vector3d> actual,
        double tolerance = 1e-9)
    {
        List<Vector3d> remaining = [.. actual];
        foreach (Vector3d point in expected)
        {
            int index = remaining.FindIndex(candidate => (candidate - point).Length() <= tolerance);
            Assert.True(index >= 0, $"В построении нет точки {point}.");
            remaining.RemoveAt(index);
        }

        Assert.Empty(remaining);
    }

    /// <summary>Все точки кольца на расстоянии радиуса от центра (в плоскости XY).</summary>
    public static void AssertRingRadius(
        IReadOnlyList<Vector3d> ring,
        Vector3d center,
        double radius,
        double tolerance = 1e-9)
    {
        foreach (Vector3d point in ring)
        {
            Assert.True(
                Math.Abs((point - center).Length() - radius) <= tolerance,
                $"Точка {point} не на радиусе {radius} от {center}.");
        }
    }
}
