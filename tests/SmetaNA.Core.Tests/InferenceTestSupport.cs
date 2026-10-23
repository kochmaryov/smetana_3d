using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Общие фигуры и вьюпорты inference-тестов.
/// Вьюпорт «сверху»: камера в (0, 0, 100) смотрит вниз, FOV 90°, 800×800 px —
/// на плоскости z = 0 масштаб 4 px на мировую единицу.
/// </summary>
internal static class InferenceTestSupport
{
    public const double PixelsPerUnit = 4.0;

    /// <summary>Вид сверху (Z — ось взгляда; годится для осей X/Y).</summary>
    public static InferenceViewport TopDown() =>
        InferenceViewport.CreatePerspective(
            new Vector3d(0, 0, 100), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), Math.PI / 2, 800, 800);

    /// <summary>Вид спереди (Y — ось взгляда; годится для оси Z).</summary>
    public static InferenceViewport Front() =>
        InferenceViewport.CreatePerspective(
            new Vector3d(0, -100, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1), Math.PI / 2, 800, 800);

    /// <summary>Косой вид на куб: все восемь вершин проецируются в разные пиксели.</summary>
    public static InferenceViewport Oblique()
    {
        var eye = new Vector3d(100, 80, 60);
        var target = new Vector3d(10, 10, 10);
        return InferenceViewport.CreatePerspective(
            eye, (target - eye).Normalized(), new Vector3d(0, 0, 1), Math.PI / 3, 800, 800);
    }

    /// <summary>Куб 20×20×20 с началом в (0, 0, 0): 12 рёбер и 6 четырёхугольных граней.</summary>
    public static InferenceScene Cube(double size = 20)
    {
        Vector3d[] corners =
        [
            new(0, 0, 0), new(size, 0, 0), new(size, size, 0), new(0, size, 0),
            new(0, 0, size), new(size, 0, size), new(size, size, size), new(0, size, size),
        ];

        SnapSegment[] segments =
        [
            new(corners[0], corners[1]), new(corners[1], corners[2]), new(corners[2], corners[3]), new(corners[3], corners[0]),
            new(corners[4], corners[5]), new(corners[5], corners[6]), new(corners[6], corners[7]), new(corners[7], corners[4]),
            new(corners[0], corners[4]), new(corners[1], corners[5]), new(corners[2], corners[6]), new(corners[3], corners[7]),
        ];

        SnapFace[] faces =
        [
            new([corners[0], corners[3], corners[2], corners[1]]),
            new([corners[4], corners[5], corners[6], corners[7]]),
            new([corners[0], corners[1], corners[5], corners[4]]),
            new([corners[2], corners[3], corners[7], corners[6]]),
            new([corners[1], corners[2], corners[6], corners[5]]),
            new([corners[0], corners[4], corners[7], corners[3]]),
        ];

        return new InferenceScene(segments, faces);
    }

    /// <summary>Одно ребро (10, 10, 0)–(30, 10, 0), вдали от начала координат.</summary>
    public static InferenceScene SingleEdge() =>
        new([new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 10, 0))], []);

    /// <summary>Диагональное ребро (10, 10, 0)–(30, 30, 0): направление не осевое.</summary>
    public static InferenceScene DiagonalEdge() =>
        new([new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 30, 0))], []);

    /// <summary>Два пересекающихся ребра с точкой пересечения (25, 10, 0).</summary>
    public static InferenceScene CrossingEdges() =>
        new(
            [
                new SnapSegment(new Vector3d(10, 10, 0), new Vector3d(30, 10, 0)),
                new SnapSegment(new Vector3d(25, 5, 0), new Vector3d(25, 20, 0)),
            ],
            []);

    public static Vector2d Project(InferenceViewport viewport, Vector3d point)
    {
        Vector2d? projected = viewport.TryProject(point);
        return projected ?? throw new InvalidOperationException($"Точка {point} не проецируется.");
    }

    /// <summary>Запрос с курсором в мировой точке (проекция через вьюпорт).</summary>
    public static SnapRequest Request(
        InferenceViewport viewport,
        InferenceScene scene,
        Vector3d cursorWorld,
        double tolerancePx = SnapRequest.DefaultTolerancePx,
        Vector3d? anchor = null,
        SnapFilter filter = SnapFilter.All,
        SnapModifiers modifiers = SnapModifiers.None,
        IReadOnlyList<SnapGuide>? guides = null)
    {
        Vector2d cursorPx = Project(viewport, cursorWorld);
        return new SnapRequest
        {
            Ray = viewport.PickRay(cursorPx),
            Viewport = viewport,
            Scene = scene,
            CursorPx = cursorPx,
            TolerancePx = tolerancePx,
            Anchor = anchor,
            Filter = filter,
            Modifiers = modifiers,
            Guides = guides ?? Array.Empty<SnapGuide>(),
        };
    }
}

/// <summary>Сравнения inference-кандидатов с допуском.</summary>
internal static class InferenceAssert
{
    public static void PointEqual(Vector3d expected, Vector3d actual, double tolerance = 1e-6)
    {
        Assert.True(
            (expected - actual).Length() <= tolerance,
            $"Ожидалась точка {expected}, получена {actual}.");
    }

    public static void DirectionEqual(Vector3d expected, Vector3d actual, double tolerance = 1e-6)
    {
        Assert.True(
            (expected.Normalized() - actual.Normalized()).Length() <= tolerance,
            $"Ожидалось направление {expected}, получено {actual}.");
    }

    public static SnapCandidate Single(SnapResult result, SnapType type)
    {
        SnapCandidate[] found = [.. result.Candidates.Where(c => c.Type == type)];
        Assert.True(found.Length > 0, $"В кандидатах нет привязки {type}.");
        return found[0];
    }

    public static void None(SnapResult result, SnapType type)
        => Assert.DoesNotContain(result.Candidates, c => c.Type == type);
}
