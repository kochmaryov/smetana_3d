using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
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

        _localization.CultureChanged += InvalidateAll;
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
    public double GridStep
    {
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
        _showSnapLabels = normalized.ShowLabels;
        _lengthSnapping = normalized.LengthSnappingEnabled;
        InferenceSettings = Tools.Context.Inference = normalized;

        OnPropertyChanged(nameof(ShowHiddenGeometry));
        OnPropertyChanged(nameof(GridSnapping));
        OnPropertyChanged(nameof(GridStep));
        OnPropertyChanged(nameof(ShowSnapLabels));
        OnPropertyChanged(nameof(LengthSnapping));
        InferenceSettingsChanged?.Invoke();
    }

    private void PushInferenceSettings()
    {
        InferenceSettings = Tools.Context.Inference = Tools.Context.Inference with
        {
            ShowHiddenGeometry = _showHiddenGeometry,
            GridEnabled = _gridSnapping,
            GridStep = _gridStep,
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

    public string Title =>
        string.Format(_localization.Get(LocalizationKeys.ProjectName), Number);
}
