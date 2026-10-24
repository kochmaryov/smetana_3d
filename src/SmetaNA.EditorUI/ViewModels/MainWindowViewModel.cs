using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.Tools;
using SmetaNA.EditorUI.Tools.Drawing;
using SmetaNA.EditorUI.Tools.Editing;
using SmetaNA.EditorUI.Annotations;
using SmetaNA.EditorUI.Tutorial;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// View-модель главного окна: лента инструментов-заглушек, панели,
/// undo/redo поверх <see cref="SmetaNA.Core.Undo.UndoStack"/>, горячие клавиши,
/// тема и локализация (спека §3.2).
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly ToolDefinition[] PrimaryToolDefinitions =
    [
        new(ShortcutCatalog.ToolSelect, LocalizationKeys.ToolSelect, null, LocalizationKeys.ToolHintSelect),
        new(ShortcutCatalog.ToolLine, LocalizationKeys.ToolLine, VcbInputKind.Length, LocalizationKeys.ToolHintLine),
        new(ShortcutCatalog.ToolRectangle, LocalizationKeys.ToolRectangle, VcbInputKind.Length, LocalizationKeys.ToolHintRectangle),
        new(ShortcutCatalog.ToolPushPull, LocalizationKeys.ToolPushPull, VcbInputKind.Length, LocalizationKeys.ToolHintPushPull),
        new(ShortcutCatalog.ToolMove, LocalizationKeys.ToolMove, VcbInputKind.Coordinate, LocalizationKeys.ToolHintMove),
        new(ShortcutCatalog.ToolEraser, LocalizationKeys.ToolEraser, null, LocalizationKeys.ToolHintEraser),
    ];

    private static readonly ToolDefinition[] AdvancedToolDefinitions =
    [
        new(ShortcutCatalog.ToolCircle, LocalizationKeys.ToolCircle, VcbInputKind.Radius, LocalizationKeys.ToolHintCircle),
        new(ShortcutCatalog.ToolPolygon, LocalizationKeys.ToolPolygon, VcbInputKind.Radius, LocalizationKeys.ToolHintPolygon),
        new(ShortcutCatalog.ToolArc, LocalizationKeys.ToolArc, VcbInputKind.Angle, LocalizationKeys.ToolHintArc),
        FromDrawingRibbon(
            RegisterDrawingTools.ToolArc3PointId,
            LocalizationKeys.ToolArc3Point,
            LocalizationKeys.ToolHintArc3Point),
        new(ShortcutCatalog.ToolFreehand, LocalizationKeys.ToolFreehand, null, LocalizationKeys.ToolHintFreehand),
        new(ShortcutCatalog.ToolOffset, LocalizationKeys.ToolOffset, VcbInputKind.Length, LocalizationKeys.ToolHintOffset),
        new(ShortcutCatalog.ToolRotate, LocalizationKeys.ToolRotate, VcbInputKind.Angle, LocalizationKeys.ToolHintRotate),
        new(ShortcutCatalog.ToolScale, LocalizationKeys.ToolScale, VcbInputKind.Length, LocalizationKeys.ToolHintScale),
        new(ShortcutCatalog.ToolSoften, LocalizationKeys.ToolSoften, VcbInputKind.Angle, LocalizationKeys.ToolHintSoften),
        new(ShortcutCatalog.ToolPaintBucket, LocalizationKeys.ToolPaintBucket, null, LocalizationKeys.ToolHintPaintBucket),
        .. SmetaNA.EditorUI.Tools.M6.RegisterM6Tools.RibbonDefinitions,
        FromDimensionDescriptor(
            RegisterDimensionTools.DimensionCommandId,
            LocalizationKeys.ToolDimension,
            VcbInputKind.Length,
            LocalizationKeys.ToolHintDimension),
        FromDimensionDescriptor(
            RegisterDimensionTools.ScaleToDimensionCommandId,
            LocalizationKeys.ToolScaleToDimension,
            VcbInputKind.Length,
            LocalizationKeys.ToolHintScaleToDimension),
    ];

    /// <summary>Определения кнопок основной строки ленты (для тестов и смоука).</summary>
    public static IReadOnlyList<ToolDefinition> PrimaryRibbonDefinitions => PrimaryToolDefinitions;

    /// <summary>Определения кнопок расширенной строки ленты (для тестов и смоука).</summary>
    public static IReadOnlyList<ToolDefinition> AdvancedRibbonDefinitions => AdvancedToolDefinitions;

    /// <summary>
    /// Кнопка из регистраций модуля рисования (<see cref="RegisterDrawingTools.RibbonDefinitions"/>):
    /// id и вид VCB берутся из модуля, имя и подсказка — из общего каталога локализации.
    /// </summary>
    private static ToolDefinition FromDrawingRibbon(string commandId, string nameKey, string hintKey)
    {
        ToolDefinition definition = RegisterDrawingTools.RibbonDefinitions
            .First(candidate => candidate.CommandId == commandId);
        return new ToolDefinition(definition.CommandId, nameKey, definition.VcbKind, hintKey);
    }

    /// <summary>
    /// Кнопка из регистраций модуля размеров (<see cref="RegisterDimensionTools"/>):
    /// id берётся из модуля, имя и подсказка — из общего каталога локализации.
    /// </summary>
    private static ToolDefinition FromDimensionDescriptor(
        string commandId,
        string nameKey,
        VcbInputKind vcbKind,
        string hintKey)
    {
        DimensionToolDescriptor descriptor = RegisterDimensionTools.CreateDescriptors()
            .First(candidate => candidate.Id == commandId);
        return new ToolDefinition(descriptor.Id, nameKey, vcbKind, hintKey);
    }

    private readonly ILocalizationService _localization;
    private readonly ThemeService _theme;
    private readonly EditorSession _session;

    private ToolStubViewModel? _activeTool;
    private string _statusText;
    private bool _isAdvancedVisible;
    private bool _isNoviceMode = true;
    private ProjectTabViewModel? _selectedProject;
    private ShortcutRowViewModel? _selectedShortcutRow;
    private string _gestureInput = string.Empty;
    private LanguageOption? _selectedLanguage;

    public MainWindowViewModel(ILocalizationService localization, ThemeService theme, EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(session);
        _localization = localization;
        _theme = theme;
        _session = session;
        SeedDemoScene(session);

        Strings = new LocalizedStrings(localization);
        _statusText = Strings.StatusReady;

        PrimaryTools = new ObservableCollection<ToolStubViewModel>(CreateTools(PrimaryToolDefinitions));
        AdvancedTools = new ObservableCollection<ToolStubViewModel>(CreateTools(AdvancedToolDefinitions));
        PaletteGroups = new ObservableCollection<PaletteGroupViewModel>(CreatePaletteGroups());

        Tutorial = new TutorialViewModel(localization, GlassTutorialLesson.Create());
        Tutorial.StateChanged += OnTutorialStateChanged;

        Languages =
        [
            new LanguageOption(localization, EditorCulture.Russian),
            new LanguageOption(localization, EditorCulture.English),
        ];
        _selectedLanguage = Languages[0];

        Projects = [];
        AddProject();

        ShortcutRows = new ObservableCollection<ShortcutRowViewModel>(
            session.Shortcuts.CommandIds.Select(id => new ShortcutRowViewModel(localization, session.Shortcuts, id)));
        _selectedShortcutRow = ShortcutRows[0];
        _gestureInput = _selectedShortcutRow.Gesture;

        UndoCommand = new RelayCommand(_ => _session.History.Undo(), _ => _session.History.CanUndo);
        RedoCommand = new RelayCommand(_ => _session.History.Redo(), _ => _session.History.CanRedo);
        NewProjectCommand = new RelayCommand(AddProject);
        ThemeToggleCommand = new RelayCommand(ToggleTheme);
        ToggleModeCommand = new RelayCommand(ToggleMode);
        OpenLicenseCommand = new RelayCommand(_ => LicenseRequested?.Invoke(this, EventArgs.Empty));
        ApplyGestureCommand = new RelayCommand(ApplyGesture);
        ResetGestureCommand = new RelayCommand(ResetGesture);
        ResetAllGesturesCommand = new RelayCommand(ResetAllGestures);

        _session.History.Changed += OnHistoryChanged;
        _localization.CultureChanged += OnCultureChanged;
        _theme.Changed += OnThemeChanged;
    }

    public LocalizedStrings Strings { get; }

    /// <summary>Контроллер инструментов сессии (маршрутизация ввода вьюпорта).</summary>
    public ToolController Tools => _session.Tools;

    /// <summary>Интерактивный урок S1 (режим «Новичок»).</summary>
    public TutorialViewModel Tutorial { get; }

    public ObservableCollection<ToolStubViewModel> PrimaryTools { get; }
    public ObservableCollection<ToolStubViewModel> AdvancedTools { get; }

    /// <summary>Группы палитры инструментов (волна 0: каркас; иконки/вылеты — волна 1).</summary>
    public ObservableCollection<PaletteGroupViewModel> PaletteGroups { get; }
    public ObservableCollection<LanguageOption> Languages { get; }
    public ObservableCollection<ProjectTabViewModel> Projects { get; }
    public ObservableCollection<ShortcutRowViewModel> ShortcutRows { get; }
    public ObservableCollection<HistoryEntryViewModel> HistoryEntries { get; } = [];

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand ThemeToggleCommand { get; }
    public RelayCommand ToggleModeCommand { get; }
    public RelayCommand OpenLicenseCommand { get; }
    public RelayCommand ApplyGestureCommand { get; }
    public RelayCommand ResetGestureCommand { get; }
    public RelayCommand ResetAllGesturesCommand { get; }

    /// <summary>Запрос открытия экрана лицензии (обрабатывается окном).</summary>
    public event EventHandler? LicenseRequested;

    /// <summary>Активный инструмент (заглушка) или <c>null</c> — нет.</summary>
    public ToolStubViewModel? ActiveTool
    {
        get => _activeTool;
        private set
        {
            if (!SetField(ref _activeTool, value))
            {
                return;
            }

            foreach (ToolStubViewModel tool in AllTools)
            {
                tool.IsActive = ReferenceEquals(tool, value);
            }

            OnPropertyChanged(nameof(ActiveToolName));
            OnPropertyChanged(nameof(ActiveToolHint));
            OnPropertyChanged(nameof(ActiveToolGesture));
            OnPropertyChanged(nameof(ActiveToolInputName));
            OnPropertyChanged(nameof(HasVcb));
            OnPropertyChanged(nameof(VcbKind));
            OnPropertyChanged(nameof(VcbPrompt));
        }
    }

    public string ActiveToolName => ActiveTool?.Name ?? Strings.ToolNone;

    /// <summary>Контекстная подсказка активного инструмента (режим «Новичок»).</summary>
    public string ActiveToolHint =>
        IsNoviceMode ? ActiveTool?.Hint ?? string.Empty : string.Empty;

    /// <summary>Горячая клавиша активного инструмента.</summary>
    public string ActiveToolGesture => ActiveTool?.Gesture ?? string.Empty;

    /// <summary>Вид числового ввода активного инструмента для панели свойств.</summary>
    public string ActiveToolInputName =>
        ActiveTool?.VcbKind is { } kind
            ? _localization.Get(LocalizationKeys.ForVcbPrompt(kind))
            : Strings.ToolNone;

    /// <summary>Режим «Новичок»: подсказки к инструментам и урок.</summary>
    public bool IsNoviceMode
    {
        get => _isNoviceMode;
        set
        {
            if (!SetField(ref _isNoviceMode, value))
            {
                return;
            }

            RefreshToolTips();
            OnPropertyChanged(nameof(ActiveToolHint));
            OnPropertyChanged(nameof(ModeToggleLabel));
            OnPropertyChanged(nameof(IsTutorialVisible));
        }
    }

    /// <summary>Подпись переключателя режима: режим, в который произойдёт переключение.</summary>
    public string ModeToggleLabel =>
        _isNoviceMode ? Strings.ModeExpert : Strings.ModeNovice;

    /// <summary>Панель урока видна в режиме «Новичок».</summary>
    public bool IsTutorialVisible => _isNoviceMode;

    public bool IsAdvancedVisible
    {
        get => _isAdvancedVisible;
        set => SetField(ref _isAdvancedVisible, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public ProjectTabViewModel? SelectedProject
    {
        get => _selectedProject;
        set => SetField(ref _selectedProject, value);
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetField(ref _selectedLanguage, value) && value is not null)
            {
                _localization.SetCulture(value.Culture);
            }
        }
    }

    public ShortcutRowViewModel? SelectedShortcutRow
    {
        get => _selectedShortcutRow;
        set
        {
            if (SetField(ref _selectedShortcutRow, value) && value is not null)
            {
                GestureInput = value.Gesture;
            }
        }
    }

    public string GestureInput
    {
        get => _gestureInput;
        set => SetField(ref _gestureInput, value);
    }

    /// <summary>Подпись кнопки темы: тема, в которую произойдёт переключение.</summary>
    public string ThemeToggleLabel =>
        _theme.Mode == ThemeMode.Dark ? Strings.ThemeLight : Strings.ThemeDark;

    public string UndoHint =>
        _session.History.NextUndoDescription is { } description
            ? string.Format(Strings.EditUndoNamed, description)
            : Strings.EditUndo;

    public string RedoHint =>
        _session.History.NextRedoDescription is { } description
            ? string.Format(Strings.EditRedoNamed, description)
            : Strings.EditRedo;

    public bool IsHistoryEmpty => HistoryEntries.Count == 0;

    /// <summary>Есть ли у активного инструмента числовой ввод (VCB).</summary>
    public bool HasVcb => ActiveTool?.VcbKind is not null;

    public VcbInputKind VcbKind => ActiveTool?.VcbKind ?? VcbInputKind.Length;

    public string VcbPrompt =>
        HasVcb
            ? _localization.Get(LocalizationKeys.ForVcbPrompt(VcbKind))
            : Strings.VcbPromptReady;

    public LengthUnit SceneUnit => _session.Scene.Unit;

    public string SceneUnitName => _localization.Get(LocalizationKeys.ForUnit(_session.Scene.Unit));

    public string SceneNodeCount =>
        Math.Max(0, _session.Scene.EnumerateNodes().Count() - 1).ToString(CultureInfo.CurrentCulture);

    /// <summary>История команд из панели горячих клавиш (для отладки/смоука).</summary>
    public int HistoryCount => _session.History.Entries.Count;

    /// <summary>Идентификатор команды, назначенной на комбинацию клавиш.</summary>
    public string? FindCommand(Shortcut gesture) =>
        _session.Shortcuts.CommandIds.FirstOrDefault(id => _session.Shortcuts.Find(id) == gesture);

    /// <summary>Инструмент-заглушка по идентификатору команды.</summary>
    public void ActivateToolById(string commandId)
    {
        ToolStubViewModel? tool = PrimaryTools.Concat(AdvancedTools)
            .FirstOrDefault(candidate => candidate.CommandId == commandId);
        if (tool is null)
        {
            StatusText = Strings.ShortcutsErrorUnknown;
            return;
        }

        ActivateTool(tool);
    }

    /// <summary>Выполнить команду по её идентификатору (используется горячими клавишами).</summary>
    public void RunCommand(string commandId)
    {
        switch (commandId)
        {
            case ShortcutCatalog.ProjectNew:
                AddProject();
                break;
            case ShortcutCatalog.EditUndo:
                _session.History.Undo();
                break;
            case ShortcutCatalog.EditRedo:
                _session.History.Redo();
                break;
            case ShortcutCatalog.ViewToggleTheme:
                ToggleTheme();
                break;
            case ShortcutCatalog.ViewToggleLanguage:
                ToggleLanguage();
                break;
            default:
                ActivateToolById(commandId);
                break;
        }
    }

    public void ToggleTheme()
    {
        _theme.Toggle();
    }

    /// <summary>Переключить режим «Новичок»/«Профи».</summary>
    public void ToggleMode()
    {
        IsNoviceMode = !IsNoviceMode;
        StatusText = string.Format(
            Strings.ModeSwitched,
            IsNoviceMode ? Strings.ModeNovice : Strings.ModeExpert);
    }

    public void ToggleLanguage() =>
        _localization.SetCulture(
            _localization.Culture == EditorCulture.Russian
                ? EditorCulture.English
                : EditorCulture.Russian);

    /// <summary>Разобрать текст из VCB и применить его (путь, общий с контролом).</summary>
    public bool CommitVcbText(string text)
    {
        if (!HasVcb)
        {
            return false;
        }

        if (VcbKind == VcbInputKind.Coordinate)
        {
            VcbCoordinateResult coordinate = VcbValueParser.ParseCoordinate(text, SceneUnit);
            if (!coordinate.IsSuccess)
            {
                StatusText = _localization.Get(LocalizationKeys.ForVcbError(coordinate.Error));
                return false;
            }

            OnVcbCommitted(coordinate);
            return true;
        }

        VcbParseResult scalar = VcbValueParser.ParseScalar(text, VcbKind, SceneUnit);
        if (!scalar.IsSuccess)
        {
            StatusText = _localization.Get(LocalizationKeys.ForVcbError(scalar.Error));
            return false;
        }

        OnVcbCommitted(scalar);
        return true;
    }

    /// <summary>Принять разобранное скалярное значение из VCB.</summary>
    public void OnVcbCommitted(VcbParseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return;
        }

        string display = FormatScalar(result);
        if (_session.Tools.IsActive)
        {
            // значение потребляет активный инструмент (Push/Pull и др.);
            // не потребленное значение фиксируем командой в истории
            bool consumed = result.ValueInMillimeters is { } millimeters
                && _session.Tools.CommitValue(millimeters);

            StatusText = string.Format(Strings.VcbCommitted, display);
            if (!consumed)
            {
                PushStubCommand(display);
            }

            return;
        }

        StatusText = string.Format(Strings.VcbCommitted, display);
        PushStubCommand(display);
    }

    /// <summary>Принять разобранные координаты из VCB.</summary>
    public void OnVcbCommitted(VcbCoordinateResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return;
        }

        string display = string.Format(
            CultureInfo.CurrentCulture,
            "{0}; {1}; {2}",
            VcbValueParser.FormatValue(result.X),
            VcbValueParser.FormatValue(result.Y),
            VcbValueParser.FormatValue(result.Z));
        StatusText = string.Format(Strings.VcbCommitted, display);
        PushStubCommand(display);
    }

    private string FormatScalar(VcbParseResult result)
    {
        if (result.ValueInMillimeters is { } millimeters)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1}",
                VcbValueParser.FormatValue(millimeters),
                Strings.UnitMillimeter);
        }

        return VcbValueParser.FormatValue(result.Value) + "°";
    }

    private void PushStubCommand(string display) =>
        _session.History.Execute(
            new StubEditCommand(string.Format(Strings.HistoryStubCommand, display)));

    private void AddProject()
    {
        var project = new ProjectTabViewModel(_localization, _session.Scene, Projects.Count + 1, _session.Tools);
        Projects.Add(project);
        SelectedProject = project;
    }

    /// <summary>
    /// Демо-сцена ~500 000 треугольников для замеров производительности (задача 1.6):
    /// настоящая иерархия сцены, поэтому работает пиккинг и пообъектное выделение.
    /// </summary>
    private static void SeedDemoScene(EditorSession session)
    {
        Stopwatch clock = Stopwatch.StartNew();
        IReadOnlyList<MeshNode> chunks = DemoSceneFactory.AddTerrainToScene(session.Scene);
        int triangles = chunks.Sum(chunk => chunk.Mesh.Faces.Count * 2); // квады → 2 треугольника
        Console.WriteLine(
            $"[DEMO] terrain: {chunks.Count} chunks, {triangles} triangles, {clock.ElapsedMilliseconds} ms");
    }

    private void ApplyGesture()
    {
        if (_selectedShortcutRow is null)
        {
            return;
        }

        if (!Shortcut.TryParse(GestureInput, out Shortcut gesture))
        {
            StatusText = Strings.ShortcutsErrorInvalid;
            return;
        }

        ShortcutMapResult result = _session.Shortcuts.TrySet(_selectedShortcutRow.CommandId, gesture);
        if (!result.IsSuccess)
        {
            StatusText = result.Error == ShortcutMapError.Conflict
                ? string.Format(Strings.ShortcutsErrorConflict, NameOfCommand(result.ConflictCommandId))
                : Strings.ShortcutsErrorUnknown;
            return;
        }

        RefreshShortcutRows();
        StatusText = string.Format(Strings.ShortcutsApplied, _selectedShortcutRow.Name, gesture.ToString());
    }

    private void ResetGesture()
    {
        if (_selectedShortcutRow is null)
        {
            return;
        }

        _session.Shortcuts.Reset(_selectedShortcutRow.CommandId);
        RefreshShortcutRows();
        GestureInput = _selectedShortcutRow.Gesture;
        StatusText = string.Format(Strings.ShortcutsApplied, _selectedShortcutRow.Name, _selectedShortcutRow.Gesture);
    }

    private void ResetAllGestures()
    {
        _session.Shortcuts.ResetAll();
        RefreshShortcutRows();
        if (_selectedShortcutRow is not null)
        {
            GestureInput = _selectedShortcutRow.Gesture;
        }

        StatusText = Strings.ShortcutsResetAll;
    }

    private string NameOfCommand(string? commandId) =>
        ShortcutRows.FirstOrDefault(row => row.CommandId == commandId)?.Name ?? string.Empty;

    private void RefreshShortcutRows()
    {
        foreach (ShortcutRowViewModel row in ShortcutRows)
        {
            row.Refresh();
        }
    }

    private IEnumerable<ToolStubViewModel> CreateTools(IEnumerable<ToolDefinition> definitions) =>
        CreateTools(definitions, ActivateTool);

    private IEnumerable<ToolStubViewModel> CreateTools(
        IEnumerable<ToolDefinition> definitions,
        Action<ToolStubViewModel> activate) =>
        definitions.Select(definition =>
            new ToolStubViewModel(
                _localization,
                _session.Shortcuts,
                definition,
                activate,
                () => _isNoviceMode));

    /// <summary>Группы палитры из реестра (кнопки-заглушки: подпись = жест).</summary>
    private IEnumerable<PaletteGroupViewModel> CreatePaletteGroups() =>
        ToolGroupRegistry.DefaultGroups.Select(group =>
            new PaletteGroupViewModel(
                _localization,
                group,
                CreateTools(group.Buttons.Select(button => button.Definition), ActivatePaletteItem)));

    /// <summary>Активация кнопки палитры: служебные команды — свои, инструменты — как с ленты.</summary>
    private void ActivatePaletteItem(ToolStubViewModel tool)
    {
        switch (tool.CommandId)
        {
            case ToolPaletteCommandIds.PrintVolume:
                StatusText = tool.Name;
                break;
            case ToolPaletteCommandIds.Tutorial:
                Tutorial.StartCommand.Execute(null);
                break;
            default:
                ActivateTool(tool);
                break;
        }
    }

    /// <summary>Все кнопки инструментов ленты и палитры.</summary>
    public IEnumerable<ToolStubViewModel> AllTools =>
        PrimaryTools.Concat(AdvancedTools).Concat(PaletteGroups.SelectMany(group => group.Buttons));

    private void RefreshToolTips()
    {
        foreach (ToolStubViewModel tool in AllTools)
        {
            tool.Refresh();
        }
    }

    private void OnTutorialStateChanged()
    {
        string? highlight = Tutorial.HighlightedCommandId;
        foreach (ToolStubViewModel tool in AllTools)
        {
            tool.IsHighlighted = highlight is not null && tool.CommandId == highlight;
        }

        if (Tutorial.IsActive)
        {
            StatusText = Tutorial.CurrentStepText;
        }

        OnPropertyChanged(nameof(IsTutorialVisible));
    }

    private void ActivateTool(ToolStubViewModel tool)
    {
        if (tool.CommandId == ShortcutCatalog.ToolPushPull)
        {
            _session.Tools.Activate(new PushPullTool());
        }
        else if (!TryActivateModuleTool(tool.CommandId))
        {
            _session.Tools.Deactivate();
        }

        ActiveTool = tool;
        StatusText = string.Format(Strings.ToolStubStatus, tool.Name);
    }

    private bool TryActivateModuleTool(string commandId)
    {
        if (RegisterDrawingTools.TryActivate(_session.Tools, commandId))
        {
            return true;
        }

        if (RegisterEditingTools.Create(commandId) is { } editingTool)
        {
            _session.Tools.Activate(editingTool);
            return true;
        }

        foreach (DimensionToolDescriptor descriptor in RegisterDimensionTools.CreateDescriptors())
        {
            if (descriptor.Id == commandId)
            {
                _session.Tools.Activate(descriptor.Create());
                return true;
            }
        }

        if (SmetaNA.EditorUI.Tools.M6.RegisterM6Tools.TryActivate(_session.Tools, commandId))
        {
            return true;
        }

        return false;
    }

    private void OnHistoryChanged()
    {
        HistoryEntries.Clear();
        IReadOnlyList<UndoHistoryEntry> entries = _session.History.Entries;
        int applied = _session.History.EntriesAppliedCount;
        for (int i = 0; i < entries.Count; i++)
        {
            HistoryEntries.Add(new HistoryEntryViewModel(entries[i].Description, Strings.HistoryCurrent, i == applied - 1));
        }

        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(UndoHint));
        OnPropertyChanged(nameof(RedoHint));
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void OnCultureChanged()
    {
        OnHistoryChanged();
        RefreshToolTips();
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(ActiveToolHint));
        OnPropertyChanged(nameof(ActiveToolGesture));
        OnPropertyChanged(nameof(ActiveToolInputName));
        OnPropertyChanged(nameof(VcbPrompt));
        OnPropertyChanged(nameof(SceneUnitName));
        OnPropertyChanged(nameof(ThemeToggleLabel));
        OnPropertyChanged(nameof(ModeToggleLabel));
        OnPropertyChanged(nameof(UndoHint));
        OnPropertyChanged(nameof(RedoHint));
        StatusText = string.Format(
            Strings.LanguageSwitched,
            _localization.Get(
                _localization.Culture == EditorCulture.Russian
                    ? LocalizationKeys.LanguageRussian
                    : LocalizationKeys.LanguageEnglish));
    }

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(ThemeToggleLabel));
        StatusText = string.Format(
            Strings.ThemeSwitched,
            _theme.Mode == ThemeMode.Dark ? Strings.ThemeDark : Strings.ThemeLight);
    }
}
