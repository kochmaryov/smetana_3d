using System;
using Avalonia;

namespace Spike.Gl;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        SpikeOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // --wgl: нативный Windows OpenGL вместо дефолтного ANGLE (GLES).
        // На других ОС флаг игнорируется — код остаётся кроссплатформенным.
        if (OperatingSystem.IsWindows() && SpikeOptions.ForceWgl)
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Wgl, Win32RenderingMode.Software },
            });
        }

        return builder;
    }
}

internal static class SpikeOptions
{
    /// <summary>0 — обычный интерактивный запуск; >0 — авто-закрытие через N секунд (GUI-смоук).</summary>
    public static double SmokeSeconds { get; private set; }

    public static bool ForceWgl { get; private set; }

    public static string ModeLabel { get; private set; } = "default";

    public static void Parse(string[] args)
    {
        double smoke = 0;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--smoke":
                    smoke = 8;
                    if (i + 1 < args.Length &&
                        double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double seconds))
                    {
                        smoke = seconds;
                        i++;
                    }
                    break;
                case "--wgl":
                    ForceWgl = true;
                    break;
            }
        }

        SmokeSeconds = smoke;
        ModeLabel = ForceWgl ? "wgl" : "default(AngleEgl+Software fallback)";
    }
}
