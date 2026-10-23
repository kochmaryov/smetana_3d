using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace SmetaNA.EditorUI.Smoke;

/// <summary>
/// Смоук-запуск: окно открывается, проверяются переключение темы/локализации
/// и связка VCB → история → undo/redo, затем приложение закрывается само.
/// </summary>
public static class SmokeCheck
{
    public static async Task RunAsync(MainWindow window, SmokeOptions options)
    {
        ArgumentNullException.ThrowIfNull(window);
        await Task.Delay(TimeSpan.FromSeconds(options.Seconds));

        SmokeReport report = window.RunSmokeChecks();
        Console.WriteLine(report.Summary);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(report.Passed ? 0 : 1);
        }
    }
}
