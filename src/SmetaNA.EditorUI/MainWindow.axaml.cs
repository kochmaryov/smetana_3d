using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Smoke;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.EditorUI.Vcb;

namespace SmetaNA.EditorUI;

/// <summary>Главное окно редактора: сборка ленты, панелей, VCB и вкладок.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(EditorServices.Localization, EditorServices.Theme, new EditorSession());
        DataContext = ViewModel;
    }

    internal MainWindowViewModel ViewModel { get; }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // ввод в полях VCB и редакторах маппингов не перехватывается
        if (e.Source is TextBox)
        {
            return;
        }

        if (!AvaloniaShortcut.TryConvert(e.Key, e.KeyModifiers, out Shortcut shortcut))
        {
            return;
        }

        string? commandId = ViewModel.FindCommand(shortcut);
        if (commandId is null)
        {
            return;
        }

        ViewModel.RunCommand(commandId);
        e.Handled = true;
    }

    private void OnVcbCommitted(object? sender, VcbCommittedEventArgs e)
    {
        if (e.Scalar is { } scalar)
        {
            ViewModel.OnVcbCommitted(scalar);
        }
        else if (e.Coordinate is { } coordinate)
        {
            ViewModel.OnVcbCommitted(coordinate);
        }
    }

    /// <summary>
    /// Смоук-проверка окна: переключение темы и языка, связка
    /// «VCB → история → undo/redo»; итог печатается в stdout.
    /// </summary>
    internal SmokeReport RunSmokeChecks()
    {
        bool opened = IsVisible;

        var themeBefore = Application.Current?.RequestedThemeVariant;
        ViewModel.ToggleTheme();
        var themeAfter = Application.Current?.RequestedThemeVariant;
        bool themeChanged = themeBefore is not null && !Equals(themeBefore, themeAfter);

        string titleBefore = Title ?? string.Empty;
        ViewModel.ToggleLanguage();
        string titleAfter = Title ?? string.Empty;
        bool languageChanged = !string.Equals(titleBefore, titleAfter, StringComparison.Ordinal);

        ViewModel.ActivateToolById(ShortcutCatalog.ToolLine);
        bool vcbCommitted = ViewModel.CommitVcbText("100");
        bool historyWired = vcbCommitted && ViewModel.HistoryCount == 1 && ViewModel.UndoCommand.CanExecute(null);
        ViewModel.UndoCommand.Execute(null);
        bool undoRedoWired = ViewModel.RedoCommand.CanExecute(null);

        bool passed = opened && themeChanged && languageChanged && vcbCommitted && historyWired && undoRedoWired;
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "[SMOKE] opened={0} themeChanged={1} languageChanged={2} vcbCommitted={3} historyWired={4} undoRedoWired={5} title='{6}' -> '{7}' status='{8}' result={9}",
            opened,
            themeChanged,
            languageChanged,
            vcbCommitted,
            historyWired,
            undoRedoWired,
            titleBefore,
            titleAfter,
            ViewModel.StatusText,
            passed ? "PASS" : "FAIL");
        return new SmokeReport(passed, summary);
    }
}
