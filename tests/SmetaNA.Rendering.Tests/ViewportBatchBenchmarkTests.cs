using System.Diagnostics;
using SmetaNA.Core.Model;
using SmetaNA.Rendering.PrintVolume;
using Xunit;
using Xunit.Abstractions;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Бенчмарк CPU-пути вьюпорта на демо-сцене 500 000 треугольников
/// (DoD §5.6): построение модели рендера, батчинг в GPU-буферы и оверлей
/// зоны печати. Сам FPS замеряется GUI-прогоном (docs/dod-measurements.md);
/// здесь — бюджет подготовки кадра, чтобы регрессии ловились в тестах.
/// </summary>
[Collection("benchmarks")]
public sealed class ViewportBatchBenchmarkTests
{
    private const double BuildBudgetMs = 5000;

    private readonly ITestOutputHelper _output;

    public ViewportBatchBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DemoScene500k_BuildAndBatch_WithinBudget()
    {
        var scene = new Scene();
        DemoSceneFactory.AddTerrainToScene(scene);

        var buildClock = Stopwatch.StartNew();
        SceneRenderModel model = SceneRenderModel.Build(scene);
        buildClock.Stop();

        var batchClock = Stopwatch.StartNew();
        RenderBatch batch = SceneBatcher.Build(model.Objects);
        batchClock.Stop();

        Assert.Equal(DemoSceneFactory.DefaultTargetTriangles, model.TriangleCount);
        Assert.Equal(model.TriangleCount, batch.TriangleCount);
        double totalMs = buildClock.Elapsed.TotalMilliseconds + batchClock.Elapsed.TotalMilliseconds;
        _output.WriteLine(
            $"[BENCH] демо-сцена {model.TriangleCount} треугольников: " +
            $"SceneRenderModel.Build={buildClock.Elapsed.TotalMilliseconds:F1} мс, " +
            $"SceneBatcher.Build={batchClock.Elapsed.TotalMilliseconds:F1} мс, " +
            $"итого={totalMs:F1} мс");
        Assert.True(totalMs <= BuildBudgetMs, $"Подготовка кадра заняла {totalMs:F0} мс.");
    }

    [Fact]
    public void DemoScene500k_PrintVolumeOverlay_WithinBudget()
    {
        var scene = new Scene();
        DemoSceneFactory.AddTerrainToScene(scene);
        SceneRenderModel model = SceneRenderModel.Build(scene);
        PrinterProfile profile = PrinterProfileCatalog.Find(PrinterProfileCatalog.PrusaI3Mk3sId)!;

        var clock = Stopwatch.StartNew();
        PrintVolumeOverlay overlay = PrintVolumeOverlay.Build(profile, model);
        clock.Stop();

        _output.WriteLine(
            $"[BENCH] оверлей зоны печати на {model.TriangleCount} треугольниках: " +
            $"{clock.Elapsed.TotalMilliseconds:F1} мс; за пределами зоны: " +
            $"{overlay.Fit.OutOfVolumeTriangleCount} треугольников");
        Assert.True(overlay.HasOutOfVolume, "Рельеф 20×20 м заведомо не помещается в зону печати.");
        Assert.True(clock.Elapsed.TotalMilliseconds <= BuildBudgetMs);
    }
}
