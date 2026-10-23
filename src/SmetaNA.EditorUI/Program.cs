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
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
