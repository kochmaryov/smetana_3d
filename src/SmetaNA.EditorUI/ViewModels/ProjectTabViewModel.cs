using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Sessions;
using SmetaNA.EditorUI.Tools;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Вкладка проекта: собственный документ (сцена, история, инструменты) и строки для вьюпорта.</summary>
public sealed class ProjectTabViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private UnitOption _selectedUnit;
    private RenderModeOption _selectedRenderMode;
    private bool _showGrid = true;
    private bool _showAxes = true;
    private bool _showHiddenGeometry;
    private bool _gridSnapping;
    private double _gridStep = InferenceSettings.DefaultGridStep;
    private double _tolerance = SnapRequest.DefaultTolerancePx;
    private int _categoryPresetIndex;
    private bool _showSnapLabels = true;
    private bool _lengthSnapping;
    private bool _isHiddenGeometryPreview;
    private bool _isOrthographic;
    private string _cursorCoordinates = string.Empty;

    public ProjectTabViewModel(
        ILocalizationService localization,
        EditorDocument document,
        int number)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(document);

        _localization = localization;
        Document = document;
        Number = number;

        Strings = new LocalizedStrings(localization);
        Units = new ObservableCollection<UnitOption>(
            Enum.GetValues<LengthUnit>().Select(unit => new UnitOption(localization, unit)));
        _selectedUnit = Units.First(option => option.Unit == document.Scene.Unit);

        RenderModes = new ObservableCollection<RenderModeOption>(
            Enum.GetValues<RenderMode>().Select(mode => new RenderModeOption(localization, mode)));
        _selectedRenderMode = RenderModes.First(option => option.Mode == RenderMode.Shaded);

        CycleGridStepCommand = new RelayCommand(() =>
        {
            double next = GridStepPresets.FirstOrDefault(preset => preset > _gridStep + 1e-9);
            GridStep = next > 0 ? next : GridStepPresets[0];
        });

        CycleToleranceCommand = new RelayCommand(() =>
        {
            double next = TolerancePresets.FirstOrDefault(preset => preset > _tolerance + 1e-9);
            Tolerance = next > 0 ? next : TolerancePresets[0];
        });

        CycleCategoriesCommand = new RelayCommand(() =>
        {
            _categoryPresetIndex = (_categoryPresetIndex + 1) % CategoryPresets.Count;
            OnPropertyChanged(nameof(CategoriesMenuLabel));
            PushInferenceSettings();
        });

        ToggleCoordinateSpaceCommand = new RelayCommand(() => Tools.Context.ToggleCoordinateSpace());

        _localization.CultureChanged += InvalidateAll;
        Tools.Context.SnapStateChanged += OnSnapStateChanged;
        Tools.Context.CoordinateSpaceChanged += OnCoordinateSpaceChanged;
    }

    /// <summary>Вкладка поверх существующей сцены (сцена оборачивается в новый документ).</summary>
    public ProjectTabViewModel(ILocalizationService localization, Scene scene, int number)
        : this(localization, new EditorDocument(scene), number)
    {
    }

    /// <summary>Активировать инструмент вкладки по идентификатору команды (если назначен).</summary>
    public void ActivateTool(string commandId) => ActivateToolById?.Invoke(commandId);

    public int Number { get; }

    /// <summary>Документ вкладки: сцена, история и инструменты проекта.</summary>
    public EditorDocument Document { get; }

    /// <summary>Сцена вкладки (контракты SmetaNA.Core используются только для чтения/вызова).</summary>
    public Scene Scene => Document.Scene;

    /// <summary>Контроллер инструментов вкладки.</summary>
    public ToolController Tools => Document.Tools;

    /// <summary>
    /// Активация инструмента по идентификатору команды (панель у курсора, T4);
    /// назначается главной моделью при создании вкладки.
    /// </summary>
    public Action<string>? ActivateToolById { get; set; }

    public LocalizedStrings Strings { get; }

    public ObservableCollection<UnitOption> Units { get; }

    public ObservableCollection<RenderModeOption> RenderModes { get; }

    /// <summary>Показывать сетку сцены (тумблер шапки, виджеты вьюпорта).</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set => SetField(ref _showGrid, value);
    }

    /// <summary>Показывать оси сцены (тумблер шапки, виджеты вьюпорта).</summary>
    public bool ShowAxes
    {
        get => _showAxes;
        set => SetField(ref _showAxes, value);
    }

    /// <summary>
    /// Постоянный режим «Показать скрытую геометрию» (R-10): скрытые узлы рисуются и
    /// участвуют в привязках. Временный показ — удержание Ctrl+Shift во вьюпорте.
    /// </summary>
    public bool ShowHiddenGeometry
    {
        get => _showHiddenGeometry;
        set
        {
            if (SetField(ref _showHiddenGeometry, value))
            {
                PushInferenceSettings();
            }
        }
    }

    /// <summary>Привязка к сетке с шагом из настроек (R-14), по умолчанию выключена.</summary>
    public bool GridSnapping
    {
        get => _gridSnapping;
        set
        {
            if (SetField(ref _gridSnapping, value))
            {
                PushInferenceSettings();
            }
        }
    }

    /// <summary>Шаг сетки привязки в единицах сцены.</summary>
    public double GridStep    {
        get => _gridStep;
        set
        {
            double step = double.IsFinite(value) && value > 0 ? value : InferenceSettings.DefaultGridStep;
            if (SetField(ref _gridStep, step))
            {
                OnPropertyChanged(nameof(GridStepMenuLabel));
                PushInferenceSettings();
            }
        }
    }

    /// <summary>Подпись пункта меню: текущий шаг сетки в единицах сцены (R-14).</summary>
    public string GridStepMenuLabel => string.Format(
        CultureInfo.CurrentCulture,
        Strings.ViewGridStep,
        _gridStep.ToString("0.###", CultureInfo.CurrentCulture));

    /// <summary>Пресеты шага сетки: 0,1 / 1 / 10 / 100 единиц сцены.</summary>
    public static IReadOnlyList<double> GridStepPresets { get; } = [0.1, 1, 10, 100];

    /// <summary>Пресеты толеранса привязки в пикселях.</summary>
    public static IReadOnlyList<double> TolerancePresets { get; } = [6, 8, 12];

    /// <summary>Подпись пункта меню: текущий толеранс привязки, пикселей.</summary>
    public string ToleranceMenuLabel => string.Format(
        CultureInfo.CurrentCulture,
        Strings.ViewTolerance,
        _tolerance.ToString("0.##", CultureInfo.CurrentCulture));

    /// <summary>Переключить толеранс на следующий пресет.</summary>
    public ICommand CycleToleranceCommand { get; }

    /// <summary>Пресеты категорий привязок (R-35 / настройки §6.5) с ключами имён.</summary>
    public static IReadOnlyList<(SnapFilter Filter, string NameKey)> CategoryPresets { get; } =
    [
        (SnapFilter.All, LocalizationKeys.ViewCategoriesAll),
        (SnapFilter.Points | SnapFilter.Axes, LocalizationKeys.ViewCategoriesPointsAndAxes),
        (SnapFilter.All & ~(SnapFilter.Axes | SnapFilter.InstanceAxes), LocalizationKeys.ViewCategoriesNoAxes),
        (SnapFilter.Endpoints | SnapFilter.Midpoints | SnapFilter.Centers | SnapFilter.Intersections
            | SnapFilter.OnFace | SnapFilter.OnEdges, LocalizationKeys.ViewCategoriesGeometry),
    ];

    /// <summary>Подпись пункта меню: текущий набор категорий.</summary>
    public string CategoriesMenuLabel => string.Format(
        CultureInfo.CurrentCulture,
        Strings.ViewCategories,
        Strings.Get(CategoryPresets[_categoryPresetIndex].NameKey));

    /// <summary>Переключить набор категорий привязок на следующий пресет.</summary>
    public ICommand CycleCategoriesCommand { get; }

    /// <summary>Переключить мировые/локальные координаты ввода (R-16, S14).</summary>
    public ICommand ToggleCoordinateSpaceCommand { get; }

    /// <summary>Переключить шаг сетки на следующий пресет (после 100 — снова 0,1).</summary>
    public ICommand CycleGridStepCommand { get; }

    /// <summary>Установить шаг сетки (значение из настроек или пресета).</summary>
    public void SetGridStep(double step) => GridStep = step;

    /// <summary>Показывать подписи типов привязок у курсора (R-27).</summary>
    public bool ShowSnapLabels
    {
        get => _showSnapLabels;
        set
        {
            if (SetField(ref _showSnapLabels, value))
            {
                PushInferenceSettings();
            }
        }
    }

    /// <summary>
    /// Привязка длины к точности (аналог `Enable Length Snapping`, R-14.1),
    /// по умолчанию выключена.
    /// </summary>
    public bool LengthSnapping
    {
        get => _lengthSnapping;
        set
        {
            if (SetField(ref _lengthSnapping, value))
            {
                PushInferenceSettings();
            }
        }
    }

    /// <summary>Толеранс привязки в пикселях (R-31, настройки).</summary>
    public double Tolerance
    {
        get => _tolerance;
        set
        {
            double tolerance = double.IsFinite(value) && value > 0 ? value : SnapRequest.DefaultTolerancePx;
            if (SetField(ref _tolerance, tolerance))
            {
                OnPropertyChanged(nameof(ToleranceMenuLabel));
                PushInferenceSettings();
            }
        }
    }

    /// <summary>Действующие настройки привязок вкладки (переживают смену вкладки).</summary>
    public InferenceSettings InferenceSettings { get; private set; } = InferenceSettings.Default;

    /// <summary>
    /// Временный показ скрытой геометрии удержанием Ctrl+Shift — индикатор
    /// в статус-строке (R-24).
    /// </summary>
    public bool IsHiddenGeometryPreview
    {
        get => _isHiddenGeometryPreview;
        set => SetField(ref _isHiddenGeometryPreview, value);
    }

    /// <summary>Пользователь изменил настройки привязок — их нужно сохранить.</summary>
    public event Action? InferenceSettingsChanged;

    /// <summary>Применить настройки привязок (загрузка из файла, смена вкладки).</summary>
    public void ApplyInferenceSettings(InferenceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        InferenceSettings normalized = settings.Normalize();

        _showHiddenGeometry = normalized.ShowHiddenGeometry;
        _gridSnapping = normalized.GridEnabled;
        _gridStep = normalized.GridStep;
        _tolerance = normalized.TolerancePx;
        _categoryPresetIndex = ResolveCategoryPresetIndex(normalized.Categories);
        _showSnapLabels = normalized.ShowLabels;
        _lengthSnapping = normalized.LengthSnappingEnabled;
        InferenceSettings = Tools.Context.Inference = normalized;

        OnPropertyChanged(nameof(ShowHiddenGeometry));
        OnPropertyChanged(nameof(GridSnapping));
        OnPropertyChanged(nameof(GridStep));
        OnPropertyChanged(nameof(Tolerance));
        OnPropertyChanged(nameof(CategoriesMenuLabel));
        OnPropertyChanged(nameof(ShowSnapLabels));
        OnPropertyChanged(nameof(LengthSnapping));
        InferenceSettingsChanged?.Invoke();
    }

    /// <summary>Индекс пресета категорий, совпадающего с набором; иначе — «Всё».</summary>
    private static int ResolveCategoryPresetIndex(SnapFilter filter)
    {
        for (int i = 0; i < CategoryPresets.Count; i++)
        {
            if (CategoryPresets[i].Filter == filter)
            {
                return i;
            }
        }

        return 0;
    }

    private void PushInferenceSettings()
    {
        InferenceSettings = Tools.Context.Inference = Tools.Context.Inference with
        {
            ShowHiddenGeometry = _showHiddenGeometry,
            GridEnabled = _gridSnapping,
            GridStep = _gridStep,
            TolerancePx = _tolerance,
            Categories = CategoryPresets[_categoryPresetIndex].Filter,
            ShowLabels = _showSnapLabels,
            LengthSnappingEnabled = _lengthSnapping,
        };
        InferenceSettingsChanged?.Invoke();
    }

    /// <summary>Ортогональная проекция камеры (тумблер шапки, виджеты вьюпорта).</summary>
    public bool IsOrthographic
    {
        get => _isOrthographic;
        set => SetField(ref _isOrthographic, value);
    }

    /// <summary>Режим отображения вьюпорта (каркас/штриховка/рентген).</summary>
    public RenderModeOption SelectedRenderMode
    {
        get => _selectedRenderMode;
        set
        {
            // ComboBox при пересоздании контента вкладки сбрасывает SelectedItem
            // в null — такой сброс не должен затирать текущий режим: HUD и
            // вьюпорт читают значение без проверки на null.
            if (value is null || !SetField(ref _selectedRenderMode, value))
            {
                return;
            }

            InvalidateAll();
        }
    }

    /// <summary>Единицы измерения сцены (мм/см/м/дюймы) — переключаются в HUD вьюпорта.</summary>
    public UnitOption SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            // см. SelectedRenderMode: null от привязки ComboBox игнорируется,
            // иначе HUD падал с NullReferenceException при переключении вкладок.
            if (value is null || !SetField(ref _selectedUnit, value))
            {
                return;
            }

            Scene.Unit = value.Unit;
            InvalidateAll();
        }
    }

    /// <summary>Координаты точки привязки под курсором в единицах сцены; пусто — привязки нет.</summary>
    public string CursorCoordinates
    {
        get => _cursorCoordinates;
        private set => SetField(ref _cursorCoordinates, value);
    }

    public bool HasCursorCoordinates => _cursorCoordinates.Length > 0;

    /// <summary>Обновить координатную строку по точке привязки (мировые координаты, мм).</summary>
    public void SetCursorPoint(Vector3d? point)
    {
        if (point is Vector3d world)
        {
            double scale = Scene.Unit.ToMillimeters();
            CursorCoordinates = string.Format(
                CultureInfo.CurrentCulture,
                Strings.CursorCoordinatesTemplate,
                FormatCoordinate(world.X / scale),
                FormatCoordinate(world.Y / scale),
                FormatCoordinate(world.Z / scale),
                _localization.Get(LocalizationKeys.ForUnit(Scene.Unit)));
        }
        else
        {
            CursorCoordinates = string.Empty;
        }

        OnPropertyChanged(nameof(HasCursorCoordinates));
    }

    private static string FormatCoordinate(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Текстовая индикация активной фиксации/режима привязок (R-24); пусто — ничего
    /// не зафиксировано.
    /// </summary>
    public string SnapStateText
    {
        get
        {
            var parts = new List<string>(4);
            SnapModifiers locks = Tools.Context.LockModifiers;
            if ((locks & SnapModifiers.LockAxisX) != 0)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapLockAxisX));
            }

            if ((locks & SnapModifiers.LockAxisY) != 0)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapLockAxisY));
            }

            if ((locks & SnapModifiers.LockAxisZ) != 0)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapLockAxisZ));
            }

            bool parallel = (locks & SnapModifiers.LockParallel) != 0;
            bool perpendicular = (locks & SnapModifiers.LockPerpendicular) != 0;
            if (parallel && perpendicular)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapLockParallelPerpendicular));
            }
            else if (parallel)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapParallel));
            }
            else if (perpendicular)
            {
                parts.Add(_localization.Get(LocalizationKeys.SnapPerpendicular));
            }

            parts.Add(Tools.Context.InferenceMode switch
            {
                SnapInferenceMode.PointsOnly => _localization.Get(LocalizationKeys.SnapModePointsOnly),
                SnapInferenceMode.ParallelPerpendicularOnly =>
                    _localization.Get(LocalizationKeys.SnapModeParallelPerpendicular),
                _ => string.Empty,
            });

            parts.RemoveAll(part => part.Length == 0);
            return string.Join(" · ", parts);
        }
    }

    public bool HasSnapState => SnapStateText.Length > 0;

    /// <summary>Есть контекст (выбранный контейнер) для локальных координат ввода.</summary>
    public bool HasCoordinateContext => Tools.Context.HasCoordinateContext;

    /// <summary>Пространство координат ввода: мир/локально (R-16, S14).</summary>
    public bool IsLocalCoordinateSpace => Tools.Context.CoordinateSpace == CoordinateSpace.Local;

    /// <summary>Подпись переключателя пространства координат в статус-строке.</summary>
    public string CoordinateSpaceText => _localization.Get(
        IsLocalCoordinateSpace
            ? LocalizationKeys.CoordinateSpaceLocal
            : LocalizationKeys.CoordinateSpaceWorld);

    /// <summary>Переключить мировые/локальные координаты ввода.</summary>
    public void ToggleCoordinateSpace() => Tools.Context.ToggleCoordinateSpace();

    /// <summary>
    /// Контекст локальных координат — выбранный контейнер (группа/компонент):
    /// его оси и начало используются абсолютной строкой VCB.
    /// </summary>
    public void UpdateCoordinateContext(SceneNode? node)
        => Tools.Context.SetCoordinateContext(
            node is GroupNode or ComponentInstanceNode ? node.WorldTransform() : null);

    private void OnSnapStateChanged()
    {
        OnPropertyChanged(nameof(SnapStateText));
        OnPropertyChanged(nameof(HasSnapState));
    }

    private void OnCoordinateSpaceChanged()
    {
        OnPropertyChanged(nameof(HasCoordinateContext));
        OnPropertyChanged(nameof(IsLocalCoordinateSpace));
        OnPropertyChanged(nameof(CoordinateSpaceText));
    }

    public string Title =>
        string.Format(_localization.Get(LocalizationKeys.ProjectName), Number);
}
