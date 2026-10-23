using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Smoke;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.Views;
using SmetaNA.Rendering;

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

        // Escape прерывает действие активного инструмента (сцена не меняется)
        if (e.Key == Key.Escape && ViewModel.Tools.IsActive
            && ViewModel.Tools.RouteKey(new KeyInput(ToolKey.Escape)))
        {
            e.Handled = true;
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
    /// «VCB → история → undo/redo», состояние GL-вьюпорта; итог печатается в stdout.
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

        bool viewportWired = ReportViewportStats();

        bool passed = opened && themeChanged && languageChanged && vcbCommitted && historyWired && undoRedoWired && viewportWired;
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

    /// <summary>
    /// Статистика GL-вьюпорта для смоука: кадры, FPS (средний/минимальный),
    /// размер сцены и GL-контекст. Печатает строку [VIEWPORT] в stdout.
    /// </summary>
    private bool ReportViewportStats()
    {
        ViewportView? view = this.GetVisualDescendants().OfType<ViewportView>().FirstOrDefault();
        if (view is null)
        {
            Console.WriteLine("[VIEWPORT] view=missing result=FAIL");
            return false;
        }

        ViewportStats stats = view.GetStats();
        bool ok = stats.GlEverReady && stats.TriangleCount > 0;
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "[VIEWPORT] frames={0} avgFps={1:F1} minFps={2:F1} frameMs={3:F2} triangles={4} edges={5} objects={6} glReady={7} glEverReady={8} gl='{9}' targetFps={10} mode={11} result={12}",
                stats.FramesTotal,
                stats.AverageFps,
                stats.MinBucketFps,
                stats.FrameMs,
                stats.TriangleCount,
                stats.EdgeCount,
                stats.ObjectCount,
                stats.GlReady,
                stats.GlEverReady,
                stats.GlInfo,
                EditorOptions.TargetFps,
                EditorOptions.ModeLabel,
                ok ? "OK" : "FAIL"));
        return ok;
    }
}
