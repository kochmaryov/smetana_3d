using System.Collections.ObjectModel;
using System.Globalization;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.Vcb;

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
        new(ShortcutCatalog.ToolSelect, LocalizationKeys.ToolSelect, null),
        new(ShortcutCatalog.ToolLine, LocalizationKeys.ToolLine, VcbInputKind.Length),
        new(ShortcutCatalog.ToolRectangle, LocalizationKeys.ToolRectangle, VcbInputKind.Length),
        new(ShortcutCatalog.ToolPushPull, LocalizationKeys.ToolPushPull, VcbInputKind.Length),
        new(ShortcutCatalog.ToolMove, LocalizationKeys.ToolMove, VcbInputKind.Coordinate),
        new(ShortcutCatalog.ToolEraser, LocalizationKeys.ToolEraser, null),
    ];

    private static readonly ToolDefinition[] AdvancedToolDefinitions =
    [
        new(ShortcutCatalog.ToolCircle, LocalizationKeys.ToolCircle, VcbInputKind.Radius),
        new(ShortcutCatalog.ToolPolygon, LocalizationKeys.ToolPolygon, VcbInputKind.Radius),
        new(ShortcutCatalog.ToolArc, LocalizationKeys.ToolArc, VcbInputKind.Angle),
        new(ShortcutCatalog.ToolFreehand, LocalizationKeys.ToolFreehand, null),
        new(ShortcutCatalog.ToolOffset, LocalizationKeys.ToolOffset, VcbInputKind.Length),
        new(ShortcutCatalog.ToolRotate, LocalizationKeys.ToolRotate, VcbInputKind.Angle),
        new(ShortcutCatalog.ToolScale, LocalizationKeys.ToolScale, VcbInputKind.Length),
        new(ShortcutCatalog.ToolSoften, LocalizationKeys.ToolSoften, VcbInputKind.Angle),
        new(ShortcutCatalog.ToolPaintBucket, LocalizationKeys.ToolPaintBucket, null),
    ];

    private readonly ILocalizationService _localization;
    private readonly ThemeService _theme;
    private readonly EditorSession _session;

    private ToolStubViewModel? _activeTool;
    private string _statusText;
    private bool _isAdvancedVisible;
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

        Strings = new LocalizedStrings(localization);
        _statusText = Strings.StatusReady;

        PrimaryTools = new ObservableCollection<ToolStubViewModel>(CreateTools(PrimaryToolDefinitions));
        AdvancedTools = new ObservableCollection<ToolStubViewModel>(CreateTools(AdvancedToolDefinitions));

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
        ApplyGestureCommand = new RelayCommand(ApplyGesture);
        ResetGestureCommand = new RelayCommand(ResetGesture);
        ResetAllGesturesCommand = new RelayCommand(ResetAllGestures);

        _session.History.Changed += OnHistoryChanged;
        _localization.CultureChanged += OnCultureChanged;
        _theme.Changed += OnThemeChanged;
    }

    public LocalizedStrings Strings { get; }

    public ObservableCollection<ToolStubViewModel> PrimaryTools { get; }
    public ObservableCollection<ToolStubViewModel> AdvancedTools { get; }
    public ObservableCollection<LanguageOption> Languages { get; }
    public ObservableCollection<ProjectTabViewModel> Projects { get; }
    public ObservableCollection<ShortcutRowViewModel> ShortcutRows { get; }
    public ObservableCollection<HistoryEntryViewModel> HistoryEntries { get; } = [];

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand ThemeToggleCommand { get; }
    public RelayCommand ApplyGestureCommand { get; }
    public RelayCommand ResetGestureCommand { get; }
    public RelayCommand ResetAllGesturesCommand { get; }

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

            OnPropertyChanged(nameof(ActiveToolName));
            OnPropertyChanged(nameof(HasVcb));
            OnPropertyChanged(nameof(VcbKind));
            OnPropertyChanged(nameof(VcbPrompt));
        }
    }

    public string ActiveToolName => ActiveTool?.Name ?? Strings.ToolNone;

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
        var project = new ProjectTabViewModel(_localization, Projects.Count + 1);
        Projects.Add(project);
        SelectedProject = project;
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
        definitions.Select(definition =>
            new ToolStubViewModel(_localization, _session.Shortcuts, definition, ActivateTool));

    private void ActivateTool(ToolStubViewModel tool)
    {
        ActiveTool = tool;
        StatusText = string.Format(Strings.ToolStubStatus, tool.Name);
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
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(VcbPrompt));
        OnPropertyChanged(nameof(SceneUnitName));
        OnPropertyChanged(nameof(ThemeToggleLabel));
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
