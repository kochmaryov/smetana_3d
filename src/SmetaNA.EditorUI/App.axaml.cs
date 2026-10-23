using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmetaNA.EditorUI.Smoke;

namespace SmetaNA.EditorUI;

public partial class App : Application
{
    /// <summary>Режим смоука: сколько секунд держать окно открытым; <c>null</c> — обычный запуск.</summary>
    internal static SmokeOptions? Smoke { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        EditorServices.Theme.Apply();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow window = new();
            desktop.MainWindow = window;
            if (Smoke is { } smoke)
            {
                window.Opened += async (_, _) => await SmokeCheck.RunAsync(window, smoke);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
