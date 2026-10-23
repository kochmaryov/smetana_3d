using Avalonia;
using Avalonia.Styling;

namespace SmetaNA.EditorUI.Themes;

/// <summary>Тема оформления интерфейса.</summary>
public enum ThemeMode
{
    Light,
    Dark,
}

/// <summary>
/// Переключатель тёмной/светлой темы (спека §3.2 п. 6): применяет
/// <see cref="Application.RequestedThemeVariant"/> без перезапуска.
/// </summary>
public sealed class ThemeService
{
    /// <summary>Срабатывает при смене темы.</summary>
    public event Action? Changed;

    public ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    /// <summary>Применить текущую тему к приложению.</summary>
    public void Apply()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = Mode == ThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void Toggle() => SetMode(Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark);

    public void SetMode(ThemeMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        Apply();
        Changed?.Invoke();
    }
}
