using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Smoke;
using SmetaNA.EditorUI.Views;

namespace SmetaNA.EditorUI;

public partial class App : Application
{
    /// <summary>Режим смоука: сколько секунд держать окно открытым; <c>null</c> — обычный запуск.</summary>
    internal static SmokeOptions? Smoke { get; set; }

    private ErrorDialog? _errorDialog;
    private bool _isReportingError;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        EditorServices.Theme.Apply();

        // Ошибка UI-потока не должна закрывать приложение молча: показываем
        // уведомление и продолжаем работу (необработанное исключение прежнего
        // кода оставляло пользователя с «пустым» окном или закрывало программу).
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Console.Error.WriteLine($"[ERROR] неперехваченное исключение: {args.ExceptionObject}");

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

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Console.Error.WriteLine($"[ERROR] {e.Exception}");

        if (_isReportingError)
        {
            return;
        }

        _isReportingError = true;
        try
        {
            ShowError(e.Exception);
        }
        catch (Exception dialogError)
        {
            Console.Error.WriteLine($"[ERROR] не удалось показать уведомление: {dialogError}");
        }
        finally
        {
            _isReportingError = false;
        }
    }

    /// <summary>Одно окно уведомлений: повторная ошибка обновляет его и выводит вперёд.</summary>
    private void ShowError(Exception exception)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            return;
        }

        if (_errorDialog is not { } dialog)
        {
            dialog = new ErrorDialog();
            dialog.Closed += (_, _) => _errorDialog = null;
            _errorDialog = dialog;
            dialog.Show(owner);
        }

        dialog.ShowError(
            EditorServices.Localization.Get(LocalizationKeys.ErrorUnhandled),
            exception.ToString());
        dialog.Activate();
    }
}
