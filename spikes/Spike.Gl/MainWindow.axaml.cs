using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Spike.Gl;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _uiTimer;
    private readonly DateTime _startUtc = DateTime.UtcNow;

    public MainWindow()
    {
        InitializeComponent();

        // Рендер идёт на compositor-потоке — UI обновляем только с таймера UI-потока.
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Console.WriteLine($"[Spike.Gl] window opened, mode: {SpikeOptions.ModeLabel}, " +
                          $"smoke: {(SpikeOptions.SmokeSeconds > 0 ? SpikeOptions.SmokeSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s" : "off")}");
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
        _uiTimer.Tick -= OnUiTimerTick;
        PrintSummary();
        base.OnClosed(e);
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        var viewport = Viewport;
        string fpsText = string.Create(CultureInfo.InvariantCulture,
            $"FPS: {viewport.Fps:0.0}   {viewport.FrameMs:0.00} ms   кадров: {viewport.FramesTotal}");
        FpsText.Text = fpsText;
        InfoText.Text = viewport.GlInfo;
        Title = string.Create(CultureInfo.InvariantCulture,
            $"Spike.Gl — OpenGL-вьюпорт — FPS {viewport.Fps:0.0}");

        if (SpikeOptions.SmokeSeconds > 0)
        {
            double elapsed = (DateTime.UtcNow - _startUtc).TotalSeconds;
            if (elapsed >= SpikeOptions.SmokeSeconds)
            {
                Close();
            }
        }
    }

    private void PrintSummary()
    {
        double elapsed = (DateTime.UtcNow - _startUtc).TotalSeconds;
        var viewport = Viewport;
        string result = viewport.GlEverReady && viewport.FramesTotal >= 10 ? "OK" : "FAIL";
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[SMOKE] mode={SpikeOptions.ModeLabel} elapsed={elapsed:0.00}s frames={viewport.FramesTotal} " +
            $"avgFps={viewport.AverageFps:0.0} curFps={viewport.Fps:0.0} min1sBucketFps={viewport.MinFps:0.0}"));
        Console.WriteLine($"[SMOKE] {viewport.GlInfo}");
        Console.WriteLine($"[SMOKE] result={result}");
        Console.Out.Flush();
    }
}
