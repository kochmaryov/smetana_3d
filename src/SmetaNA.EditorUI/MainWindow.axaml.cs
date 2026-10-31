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
    private LicenseWindow? _licenseWindow;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(EditorServices.Localization, EditorServices.Theme, new EditorSession());
        DataContext = ViewModel;
        ViewModel.LicenseRequested += (_, _) => OpenLicenseWindow();
        ViewModel.ToolLayoutSettingsRequested += async (_, _) =>
            await ViewModel.ToolLayout.OpenDialogAsync(this);
        LayoutUpdated += OnLayoutUpdated;
        Opened += OnFirstOpened;
    }

    /// <summary>
    /// Точка подключения адаптива правой колонки: ширина окна задаёт режим
    /// (колонка ↔ рейл). Значения кэшируются в VM, частые вызовы безопасны.
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Bounds.Width > 0)
        {
            ViewModel.RightRail.ApplyWindowWidth(Bounds.Width);
        }
    }

    internal MainWindowViewModel ViewModel { get; }

    /// <summary>Экран лицензии: показывается из меню и при старте, если лицензии нет.</summary>
    internal void OpenLicenseWindow()
    {
        if (_licenseWindow is { } open && open.IsVisible)
        {
            open.Activate();
            return;
        }

        _licenseWindow = new LicenseWindow(EditorServices.CreateLicenseViewModel());
        _licenseWindow.Closed += (_, _) => _licenseWindow = null;
        _licenseWindow.Show(this);
    }

    private void OnFirstOpened(object? sender, EventArgs e)
    {
        Opened -= OnFirstOpened;

        // в смоук-режиме модальный экран лицензии не открываем
        if (App.Smoke is not null)
        {
            return;
        }

        if (EditorServices.CreateLicenseViewModel().RequiresAttention)
        {
            OpenLicenseWindow();
        }
    }

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
        bool drawingWired = ReportToolDrawing();
        bool noviceWired = ReportNoviceMode();
        bool licenseWired = ReportLicenseStatus();

        bool passed = opened && themeChanged && languageChanged && vcbCommitted && historyWired && undoRedoWired
            && drawingWired && viewportWired && noviceWired && licenseWired;
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "[SMOKE] opened={0} themeChanged={1} languageChanged={2} vcbCommitted={3} historyWired={4} undoRedoWired={5} drawingWired={6} noviceWired={7} licenseWired={8} title='{9}' -> '{10}' status='{11}' result={12}",
            opened,
            themeChanged,
            languageChanged,
            vcbCommitted,
            historyWired,
            undoRedoWired,
            drawingWired,
            noviceWired,
            licenseWired,
            titleBefore,
            titleAfter,
            ViewModel.StatusText,
            passed ? "PASS" : "FAIL");
        return new SmokeReport(passed, summary);
    }

    /// <summary>
    /// Режим «Новичок» и урок S1: переключение режима, шаги с подсветкой кнопки.
    /// </summary>
    private bool ReportNoviceMode()
    {
        bool startsNovice = ViewModel.IsNoviceMode;
        ViewModel.ToggleMode();
        bool toggled = ViewModel.IsNoviceMode != startsNovice;
        ViewModel.IsNoviceMode = true;

        ViewModel.Tutorial.Start();
        ViewModel.Tutorial.Next();
        bool stepsRun = ViewModel.Tutorial.IsActive
            && ViewModel.Tutorial.CurrentStepIndex == 1
            && ViewModel.Tutorial.HighlightedCommandId == ShortcutCatalog.ToolCircle;
        bool highlightWired = ViewModel.AllTools.Any(tool => tool.IsHighlighted);
        ViewModel.Tutorial.Close();
        bool closed = !ViewModel.Tutorial.IsActive;

        bool passed = startsNovice && toggled && stepsRun && highlightWired && closed;
        Console.WriteLine(
            $"[NOVICE] mode={startsNovice} toggled={toggled} steps={stepsRun} highlight={highlightWired} closed={closed} result={(passed ? "PASS" : "FAIL")}");
        return passed;
    }

    /// <summary>Экран лицензии: статус, триал и устройство доступны без сети; окно открывается.</summary>
    private bool ReportLicenseStatus()
    {
        try
        {
            var viewModel = EditorServices.CreateLicenseViewModel();
            bool ok = viewModel.StatusText.Length > 0
                && viewModel.TrialText.Length > 0
                && viewModel.DeviceHash.Length > 0;

            // разметка экрана лицензии должна отрабатывать без ошибок
            OpenLicenseWindow();
            bool windowShown = _licenseWindow?.IsVisible == true;
            if (_licenseWindow is { } window)
            {
                _licenseWindow = null;
                window.Close();
            }

            ok = ok && windowShown;
            Console.WriteLine(
                $"[LICENSE] status='{viewModel.StatusText}' trial='{viewModel.TrialText}' devKey={viewModel.IsDevKey} window={windowShown} result={(ok ? "OK" : "FAIL")}");
            return ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LICENSE] result=FAIL error={ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Рисование мышью через путь ввода инструментов (два клика + Enter),
    /// печатает строку [DRAWING] в stdout.
    /// </summary>
    private bool ReportToolDrawing()
    {
        ViewportView? view = this.GetVisualDescendants().OfType<ViewportView>().FirstOrDefault();
        return view?.RunToolDrawingSmoke() == true;
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
