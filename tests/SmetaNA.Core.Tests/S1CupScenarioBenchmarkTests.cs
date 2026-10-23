using System.Diagnostics;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Tools.Drawing;
using SmetaNA.Core.Undo;
using Xunit;
using Xunit.Abstractions;

namespace SmetaNA.Core.Tests;

/// <summary>
/// Бенчмарк сценария S1 «Первое знакомство» (DoD §5.2): последовательность
/// инструментов «стакан» — вдавливание полости, подгонка дна и разметка
/// окружностью. Замер времени каждого шага и суммарно; бюджет — страховка
/// от регрессий (машина не эталонная, см. docs/dod-measurements.md).
/// </summary>
[Collection("benchmarks")]
public sealed class S1CupScenarioBenchmarkTests
{
    private const double CupSizeMm = 60;
    private const double CupHeightMm = 80;
    private const double CupWallMm = 5;
    private const double CavityDepthMm = 75;
    private const double BottomAdjustMm = 2;
    private const double MarkRadiusMm = 20;

    /// <summary>Суммарный бюджет сценария, мс.</summary>
    private const double ScenarioBudgetMs = 2000;

    private readonly ITestOutputHelper _output;

    public S1CupScenarioBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void S1_CupToolSequence_RunsWithinBudget()
    {
        HalfEdgeMesh mesh = CupBlank();
        (Scene scene, MeshNode node) = ToolsTestSupport.SceneWith(mesh);
        (ToolContext context, UndoStack undo) = ToolsTestSupport.ContextFor(scene);
        // камера сверху-спереди: луч в центр дна не перекрыт бортом
        context.Viewport = ToolsTestSupport.ViewportFrom(
            new Vector3d(30, -30, 250), new Vector3d(30, 30, 0));

        var total = Stopwatch.StartNew();

        // Шаг 1 — полость: вдавливание внутренней грани на 75 мм (дно 5 мм).
        var cavityClock = Stopwatch.StartNew();
        var cavityTool = new PushPullTool();
        cavityTool.Activate(context);
        Vector2d cavityPress = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(30, 30, 80));
        Assert.True(cavityTool.OnPointer(PointerInput.Press(cavityPress, PointerButton.Left)));
        Vector2d cavityDrag = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(30, 30, 5));
        Assert.True(cavityTool.OnPointer(PointerInput.Move(cavityDrag)));
        Assert.True(cavityTool.TryApplyValue(CavityDepthMm));
        cavityTool.Deactivate();
        cavityClock.Stop();

        Assert.True(mesh.IsWatertight, "После вдавливания полости меш должен остаться замкнутым.");
        Assert.Equal(5, BottomZ(mesh), 6);
        Assert.True(undo.CanUndo);

        // Шаг 2 — подгонка дна: +2 мм (дно 7 мм).
        var bottomClock = Stopwatch.StartNew();
        var bottomTool = new PushPullTool();
        bottomTool.Activate(context);
        Vector2d bottomPress = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(30, 30, 5));
        Assert.True(bottomTool.OnPointer(PointerInput.Press(bottomPress, PointerButton.Left)));
        Vector2d bottomDrag = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(30, 30, 7));
        Assert.True(bottomTool.OnPointer(PointerInput.Move(bottomDrag)));
        Assert.True(bottomTool.TryApplyValue(BottomAdjustMm));
        bottomTool.Deactivate();
        bottomClock.Stop();

        Assert.True(mesh.IsWatertight, "После подгонки дна меш должен остаться замкнутым.");
        Assert.Equal(7, BottomZ(mesh), 6);

        // Шаг 3 — разметка дна окружностью R20 мм (рисование по грани).
        var markClock = Stopwatch.StartNew();
        int facesBefore = mesh.Faces.Count;
        var markTool = new CircleTool();
        markTool.Activate(context);
        Vector2d markPress = ToolsTestSupport.PixelOf(context.Viewport, new Vector3d(30, 30, 7));
        Assert.True(markTool.OnPointer(PointerInput.Press(markPress, PointerButton.Left)));
        Assert.True(markTool.TryApplyValue(MarkRadiusMm));
        markTool.Deactivate();
        markClock.Stop();

        Assert.Equal(facesBefore + 1, mesh.Faces.Count);

        // Инструмент сбрасывает радиус после коммита — проверяем геометрию грани.
        MeshFace markFace = mesh.Faces[^1];
        foreach (MeshVertex vertex in mesh.GetRing(markFace))
        {
            double radius = Math.Sqrt(
                ((vertex.Position.X - 30) * (vertex.Position.X - 30))
                + ((vertex.Position.Y - 30) * (vertex.Position.Y - 30)));
            Assert.Equal(MarkRadiusMm, radius, 4);
            Assert.Equal(7, vertex.Position.Z, 6);
        }

        double totalMs = total.Elapsed.TotalMilliseconds;

        _output.WriteLine(
            $"[S1] шаги: полость={cavityClock.Elapsed.TotalMilliseconds:F3} мс, " +
            $"дно={bottomClock.Elapsed.TotalMilliseconds:F3} мс, " +
            $"разметка={markClock.Elapsed.TotalMilliseconds:F3} мс; " +
            $"итого={totalMs:F3} мс (бюджет {ScenarioBudgetMs:F0} мс)");
        Assert.True(
            totalMs <= ScenarioBudgetMs,
            $"Сценарий S1 занял {totalMs:F1} мс при бюджете {ScenarioBudgetMs:F0} мс.");

        // История: три команды откатываются до заготовки.
        undo.Undo();
        undo.Undo();
        undo.Undo();
        Assert.False(undo.CanUndo);
        Assert.Equal(CupHeightMm, BottomZ(mesh), 6);
    }

    /// <summary>Заготовка «стакана»: брус 60×60×80 мм с разрезанной верхней гранью.</summary>
    private static HalfEdgeMesh CupBlank()
    {
        double s = CupSizeMm;
        double h = CupHeightMm;
        double w = CupWallMm;
        var mesh = new HalfEdgeMesh();

        MeshVertex b0 = mesh.AddVertex(new Vector3d(0, 0, 0));
        MeshVertex b1 = mesh.AddVertex(new Vector3d(s, 0, 0));
        MeshVertex b2 = mesh.AddVertex(new Vector3d(s, s, 0));
        MeshVertex b3 = mesh.AddVertex(new Vector3d(0, s, 0));

        MeshVertex t0 = mesh.AddVertex(new Vector3d(0, 0, h));
        MeshVertex t1 = mesh.AddVertex(new Vector3d(s, 0, h));
        MeshVertex t2 = mesh.AddVertex(new Vector3d(s, s, h));
        MeshVertex t3 = mesh.AddVertex(new Vector3d(0, s, h));

        MeshVertex i0 = mesh.AddVertex(new Vector3d(w, w, h));
        MeshVertex i1 = mesh.AddVertex(new Vector3d(s - w, w, h));
        MeshVertex i2 = mesh.AddVertex(new Vector3d(s - w, s - w, h));
        MeshVertex i3 = mesh.AddVertex(new Vector3d(w, s - w, h));

        mesh.AddFace([b0, b3, b2, b1]);
        mesh.AddFace([b0, b1, t1, t0]);
        mesh.AddFace([b1, b2, t2, t1]);
        mesh.AddFace([b2, b3, t3, t2]);
        mesh.AddFace([b3, b0, t0, t3]);
        mesh.AddFace([t0, t1, i1, i0]);
        mesh.AddFace([t1, t2, i2, i1]);
        mesh.AddFace([t2, t3, i3, i2]);
        mesh.AddFace([t3, t0, i0, i3]);
        mesh.AddFace([i0, i1, i2, i3]);
        return mesh;
    }

    /// <summary>Z дна полости (минимальная Z вершин внутри контура заготовки).</summary>
    private static double BottomZ(HalfEdgeMesh mesh)
    {
        double z = double.MaxValue;
        foreach (MeshVertex vertex in mesh.Vertices)
        {
            if (vertex.Position.X > 0.5 && vertex.Position.X < CupSizeMm - 0.5
                && vertex.Position.Y > 0.5 && vertex.Position.Y < CupSizeMm - 0.5
                && vertex.Position.Z < z)
            {
                z = vertex.Position.Z;
            }
        }

        return z;
    }
}
