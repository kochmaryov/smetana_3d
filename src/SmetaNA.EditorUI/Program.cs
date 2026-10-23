using Avalonia;
using SmetaNA.EditorUI.Smoke;
using System;

namespace SmetaNA.EditorUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.Smoke = SmokeOptions.Parse(args);
        EditorOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // --wgl: нативный Windows OpenGL вместо дефолтного ANGLE (GLES) — для замеров FPS.
        // На других ОС флаг игнорируется — код остаётся кроссплатформенным.
        if (OperatingSystem.IsWindows() && EditorOptions.ForceWgl)
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Wgl, Win32RenderingMode.Software },
            });
        }

        return builder;
    }
}
