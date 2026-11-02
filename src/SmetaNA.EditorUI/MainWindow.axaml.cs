using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.CommandPalette;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.FloatingGroups;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Smoke;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.Views;
using SmetaNA.Formats.ProjectFile;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI;

/// <summary>Главное окно редактора: сборка ленты, панелей, VCB и вкладок.</summary>
public partial class MainWindow : Window
{
    private LicenseWindow? _licenseWindow;
    private CommandPaletteWindow? _commandPaletteWindow;
    private readonly Dictionary<FloatingGroupViewModel, FloatingGroupWindow> _floatingWindows = [];

    // Хотбокс (T4): пробел — короткое нажатие даёт команду жеста («Выбор»),
    // удержание открывает радиальное меню; выбор — мышью или отпусканием
    // над кнопкой.
    private DispatcherTimer? _hotboxTimer;
    private bool _spaceHoldActive;

    // Жест пробела уже отработан или отменён: fallback-команда не запускается.
    private bool _spaceGestureConsumed;
    private string? _hotboxHoverTool;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel(EditorServices.Localization, EditorServices.Theme, new EditorSession());
        DataContext = ViewModel;
        ViewModel.LicenseRequested += (_, _) => OpenLicenseWindow();
        ViewModel.ToolLayoutSettingsRequested += async (_, _) =>
            await ViewModel.ToolLayout.OpenDialogAsync(this, ViewModel.FloatingGroups);
        ViewModel.OpenFileRequested += async (_, _) => await OpenProjectFileAsync();

        // Перемещаемые группы (T3): плавающие окна создаются/закрываются по
        // событиям менеджера; размещения восстанавливаются при запуске.
        ViewModel.FloatingGroups.FloatingRequested += ShowFloatingGroup;
        ViewModel.FloatingGroups.PlacementRemoved += HideFloatingGroup;
        foreach (FloatingGroupViewModel floating in ViewModel.FloatingGroups.Floating.ToList())
        {
            ShowFloatingGroup(floating);
        }
        Opened += OnFirstOpened;
    }

    internal MainWindowViewModel ViewModel { get; }

    /// <summary>Файловый диалог проекта (.sna) → открытие через view-модель.</summary>
    private async Task OpenProjectFileAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = ViewModel.Strings.ProjectOpen,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(ViewModel.Strings.ProjectFileType)
                    {
                        Patterns = [ProjectFileFormat.Extension],
                    },
                    FilePickerFileTypes.All,
                ],
            });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel.OpenProjectFile(path);
        }
    }

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

        // Ctrl+K — палитра команд (спека ui-redesign T4)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.K)
        {
            OpenCommandPalette();
            e.Handled = true;
            return;
        }

        // Пробел: удержание ≥400 мс открывает хотбокс, короткое нажатие
        // отдаёт команду жеста (по умолчанию «Выбор») в KeyUp. Автоповтор
        // клавиши таймер не перезапускает.
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            if (!_spaceHoldActive)
            {
                _spaceHoldActive = true;
                _spaceGestureConsumed = false;
                _hotboxTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _hotboxTimer.Tick += OnHotboxHoldElapsed;
                _hotboxTimer.Start();
            }

            e.Handled = true;
            return;
        }

        // Ctrl+Tab / Ctrl+W — навигация по вкладкам проектов (спека ui-redesign T7)
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Tab)
        {
            ViewModel.SelectNextProject();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.W
            && ViewModel.SelectedProject is { } project)
        {
            ViewModel.CloseProject(project);
            e.Handled = true;
            return;
        }

        // Escape закрывает хотбокс без команды (T4)
        if (e.Key == Key.Escape && HotboxPopup.IsOpen)
        {
            HotboxPopup.IsOpen = false;
            _spaceGestureConsumed = true;
            e.Handled = true;
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

    /// <summary>Палитра команд (Ctrl+K, T4): одно не-модальное окно, повтор — фокус.</summary>
    private void OpenCommandPalette()
    {
        if (_commandPaletteWindow is { } open && open.IsVisible)
        {
            open.Activate();
            return;
        }

        _commandPaletteWindow = new CommandPaletteWindow(ViewModel.CreateCommandPalette());
        _commandPaletteWindow.Closed += (_, _) => _commandPaletteWindow = null;
        _commandPaletteWindow.Show(this);
    }

    private void OnCommandPaletteClick(object? sender, RoutedEventArgs e) => OpenCommandPalette();

    /// <summary>
    /// Отпускание пробела (T4): хотбокс выбирает инструмент под курсором,
    /// короткое нажатие даёт команду жеста из карты шорткатов — по умолчанию
    /// «Выбор». KeyUp обрабатывается только для нажатий, начатых в окне
    /// (пробел на сфокусированной кнопке не должен давать команду).
    /// </summary>
    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !_spaceHoldActive)
        {
            return;
        }

        bool hotboxOpen = HotboxPopup.IsOpen;
        string? hoveredTool = _hotboxHoverTool;
        StopHotboxTimer();
        HotboxPopup.IsOpen = false;
        _hotboxHoverTool = null;

        if (hotboxOpen && hoveredTool is not null)
        {
            _spaceGestureConsumed = true;
            ViewModel.RunCommand(hoveredTool);
        }
        else if (!_spaceGestureConsumed)
        {
            RunSpaceGestureCommand();
        }

        _spaceHoldActive = false;
        _spaceGestureConsumed = false;
        e.Handled = true;
    }

    /// <summary>Команда жеста пробела из карты шорткатов (редактируемой, T12).</summary>
    private void RunSpaceGestureCommand()
    {
        if (AvaloniaShortcut.TryConvert(Key.Space, KeyModifiers.None, out Shortcut shortcut)
            && ViewModel.FindCommand(shortcut) is { } commandId)
        {
            _spaceGestureConsumed = true;
            ViewModel.RunCommand(commandId);
        }
    }

    private void OnHotboxHoldElapsed(object? sender, EventArgs e)
    {
        StopHotboxTimer();
        _hotboxHoverTool = null;
        HotboxPopup.IsOpen = true;
    }

    private void StopHotboxTimer()
    {
        if (_hotboxTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnHotboxHoldElapsed;
            _hotboxTimer = null;
        }
    }

    /// <summary>Запоминает инструмент под курсором хотбокса (выбор — отпусканием пробела).</summary>
    private void OnHotboxPointerMoved(object? sender, PointerEventArgs e)
    {
        Button? button = (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
        _hotboxHoverTool = button?.Tag as string;
    }

    /// <summary>Курсор покинул хотбокс — подсветки нет, выбор отменяется.</summary>
    private void OnHotboxPointerExited(object? sender, PointerEventArgs e) => _hotboxHoverTool = null;

    private void OnHotboxToolClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string commandId })
        {
            _spaceGestureConsumed = true;
            ViewModel.RunCommand(commandId);
        }

        HotboxPopup.IsOpen = false;
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

    /// <summary>Кнопка «×» на вкладке проекта.</summary>
    private void OnTabCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control { Tag: ProjectTabViewModel project })
        {
            ViewModel.CloseProject(project);
        }
    }

    /// <summary>Показать плавающее окно перемещаемой группы (T3).</summary>
    private void ShowFloatingGroup(FloatingGroupViewModel floating)
    {
        if (_floatingWindows.ContainsKey(floating))
        {
            return;
        }

        var window = new FloatingGroupWindow(ViewModel.FloatingGroups, floating);
        _floatingWindows[floating] = window;
        window.Show(this);
    }

    /// <summary>Закрыть плавающее окно группы (возврат в палитру / лоток).</summary>
    private void HideFloatingGroup(FloatingGroupViewModel floating)
    {
        if (_floatingWindows.Remove(floating, out FloatingGroupWindow? window))
        {
            window.Close();
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
        bool tutorialWired = ReportTutorial();
        bool licenseWired = ReportLicenseStatus();

        bool passed = opened && themeChanged && languageChanged && vcbCommitted && historyWired && undoRedoWired
            && drawingWired && viewportWired && tutorialWired && licenseWired;
        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "[SMOKE] opened={0} themeChanged={1} languageChanged={2} vcbCommitted={3} historyWired={4} undoRedoWired={5} drawingWired={6} tutorialWired={7} licenseWired={8} title='{9}' -> '{10}' status='{11}' result={12}",
            opened,
            themeChanged,
            languageChanged,
            vcbCommitted,
            historyWired,
            undoRedoWired,
            drawingWired,
            tutorialWired,
            licenseWired,
            titleBefore,
            titleAfter,
            ViewModel.StatusText,
            passed ? "PASS" : "FAIL");
        return new SmokeReport(passed, summary);
    }

    /// <summary>
    /// Урок S1: шаги, подсветка кнопки палитры и закрытие урока.
    /// </summary>
    private bool ReportTutorial()
    {
        ViewModel.Tutorial.Start();
        ViewModel.Tutorial.Next();
        bool stepsRun = ViewModel.Tutorial.IsActive
            && ViewModel.Tutorial.CurrentStepIndex == 1
            && ViewModel.Tutorial.HighlightedCommandId == ShortcutCatalog.ToolCircle;
        bool highlightWired = ViewModel.AllTools.Any(tool => tool.IsHighlighted);
        ViewModel.Tutorial.Close();
        bool closed = !ViewModel.Tutorial.IsActive;

        bool passed = stepsRun && highlightWired && closed;
        Console.WriteLine(
            $"[TUTORIAL] steps={stepsRun} highlight={highlightWired} closed={closed} result={(passed ? "PASS" : "FAIL")}");
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
