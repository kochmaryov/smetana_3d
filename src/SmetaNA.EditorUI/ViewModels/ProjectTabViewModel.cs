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

/// <summary>Р’РєР»Р°РґРєР° РїСЂРѕРµРєС‚Р°: СЃРѕР±СЃС‚РІРµРЅРЅС‹Р№ РґРѕРєСѓРјРµРЅС‚ (СЃС†РµРЅР°, РёСЃС‚РѕСЂРёСЏ, РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹) Рё СЃС‚СЂРѕРєРё РґР»СЏ РІСЊСЋРїРѕСЂС‚Р°.</summary>
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

    /// <summary>Р’РєР»Р°РґРєР° РїРѕРІРµСЂС… СЃСѓС‰РµСЃС‚РІСѓСЋС‰РµР№ СЃС†РµРЅС‹ (СЃС†РµРЅР° РѕР±РѕСЂР°С‡РёРІР°РµС‚СЃСЏ РІ РЅРѕРІС‹Р№ РґРѕРєСѓРјРµРЅС‚).</summary>
    public ProjectTabViewModel(ILocalizationService localization, Scene scene, int number)
        : this(localization, new EditorDocument(scene), number)
    {
    }

    /// <summary>РђРєС‚РёРІРёСЂРѕРІР°С‚СЊ РёРЅСЃС‚СЂСѓРјРµРЅС‚ РІРєР»Р°РґРєРё РїРѕ РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂСѓ РєРѕРјР°РЅРґС‹ (РµСЃР»Рё РЅР°Р·РЅР°С‡РµРЅ).</summary>
    public void ActivateTool(string commandId) => ActivateToolById?.Invoke(commandId);

    public int Number { get; }

    /// <summary>Р”РѕРєСѓРјРµРЅС‚ РІРєР»Р°РґРєРё: СЃС†РµРЅР°, РёСЃС‚РѕСЂРёСЏ Рё РёРЅСЃС‚СЂСѓРјРµРЅС‚С‹ РїСЂРѕРµРєС‚Р°.</summary>
    public EditorDocument Document { get; }

    /// <summary>РЎС†РµРЅР° РІРєР»Р°РґРєРё (РєРѕРЅС‚СЂР°РєС‚С‹ SmetaNA.Core РёСЃРїРѕР»СЊР·СѓСЋС‚СЃСЏ С‚РѕР»СЊРєРѕ РґР»СЏ С‡С‚РµРЅРёСЏ/РІС‹Р·РѕРІР°).</summary>
    public Scene Scene => Document.Scene;

    /// <summary>РљРѕРЅС‚СЂРѕР»Р»РµСЂ РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ РІРєР»Р°РґРєРё.</summary>
    public ToolController Tools => Document.Tools;

    /// <summary>
    /// РђРєС‚РёРІР°С†РёСЏ РёРЅСЃС‚СЂСѓРјРµРЅС‚Р° РїРѕ РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂСѓ РєРѕРјР°РЅРґС‹ (РїР°РЅРµР»СЊ Сѓ РєСѓСЂСЃРѕСЂР°, T4);
    /// РЅР°Р·РЅР°С‡Р°РµС‚СЃСЏ РіР»Р°РІРЅРѕР№ РјРѕРґРµР»СЊСЋ РїСЂРё СЃРѕР·РґР°РЅРёРё РІРєР»Р°РґРєРё.
    /// </summary>
    public Action<string>? ActivateToolById { get; set; }

    public LocalizedStrings Strings { get; }

    public ObservableCollection<UnitOption> Units { get; }

    public ObservableCollection<RenderModeOption> RenderModes { get; }

    /// <summary>РџРѕРєР°Р·С‹РІР°С‚СЊ СЃРµС‚РєСѓ СЃС†РµРЅС‹ (С‚СѓРјР±Р»РµСЂ С€Р°РїРєРё, РІРёРґР¶РµС‚С‹ РІСЊСЋРїРѕСЂС‚Р°).</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set => SetField(ref _showGrid, value);
    }

    /// <summary>РџРѕРєР°Р·С‹РІР°С‚СЊ РѕСЃРё СЃС†РµРЅС‹ (С‚СѓРјР±Р»РµСЂ С€Р°РїРєРё, РІРёРґР¶РµС‚С‹ РІСЊСЋРїРѕСЂС‚Р°).</summary>
    public bool ShowAxes
    {
        get => _showAxes;
        set => SetField(ref _showAxes, value);
    }

    /// <summary>
    /// РџРѕСЃС‚РѕСЏРЅРЅС‹Р№ СЂРµР¶РёРј В«РџРѕРєР°Р·Р°С‚СЊ СЃРєСЂС‹С‚СѓСЋ РіРµРѕРјРµС‚СЂРёСЋВ» (R-10): СЃРєСЂС‹С‚С‹Рµ СѓР·Р»С‹ СЂРёСЃСѓСЋС‚СЃСЏ Рё
    /// СѓС‡Р°СЃС‚РІСѓСЋС‚ РІ РїСЂРёРІСЏР·РєР°С…. Р’СЂРµРјРµРЅРЅС‹Р№ РїРѕРєР°Р· вЂ” СѓРґРµСЂР¶Р°РЅРёРµ Ctrl+Shift РІРѕ РІСЊСЋРїРѕСЂС‚Рµ.
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

    /// <summary>РџСЂРёРІСЏР·РєР° Рє СЃРµС‚РєРµ СЃ С€Р°РіРѕРј РёР· РЅР°СЃС‚СЂРѕРµРє (R-14), РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РІС‹РєР»СЋС‡РµРЅР°.</summary>
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

    /// <summary>РЁР°Рі СЃРµС‚РєРё РїСЂРёРІСЏР·РєРё РІ РµРґРёРЅРёС†Р°С… СЃС†РµРЅС‹.</summary>
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

    /// <summary>РџРѕРґРїРёСЃСЊ РїСѓРЅРєС‚Р° РјРµРЅСЋ: С‚РµРєСѓС‰РёР№ С€Р°Рі СЃРµС‚РєРё РІ РµРґРёРЅРёС†Р°С… СЃС†РµРЅС‹ (R-14).</summary>
    public string GridStepMenuLabel => string.Format(
        CultureInfo.CurrentCulture,
        Strings.ViewGridStep,
        _gridStep.ToString("0.###", CultureInfo.CurrentCulture));

    /// <summary>РџСЂРµСЃРµС‚С‹ С€Р°РіР° СЃРµС‚РєРё: 0,1 / 1 / 10 / 100 РµРґРёРЅРёС† СЃС†РµРЅС‹.</summary>
    public static IReadOnlyList<double> GridStepPresets { get; } = [0.1, 1, 10, 100];

    /// <summary>РџРµСЂРµРєР»СЋС‡РёС‚СЊ С€Р°Рі СЃРµС‚РєРё РЅР° СЃР»РµРґСѓСЋС‰РёР№ РїСЂРµСЃРµС‚ (РїРѕСЃР»Рµ 100 вЂ” СЃРЅРѕРІР° 0,1).</summary>
    public ICommand CycleGridStepCommand { get; }

    /// <summary>РЈСЃС‚Р°РЅРѕРІРёС‚СЊ С€Р°Рі СЃРµС‚РєРё (Р·РЅР°С‡РµРЅРёРµ РёР· РЅР°СЃС‚СЂРѕРµРє РёР»Рё РїСЂРµСЃРµС‚Р°).</summary>
    public void SetGridStep(double step) => GridStep = step;

    /// <summary>РџРѕРєР°Р·С‹РІР°С‚СЊ РїРѕРґРїРёСЃРё С‚РёРїРѕРІ РїСЂРёРІСЏР·РѕРє Сѓ РєСѓСЂСЃРѕСЂР° (R-27).</summary>
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
    /// РџСЂРёРІСЏР·РєР° РґР»РёРЅС‹ Рє С‚РѕС‡РЅРѕСЃС‚Рё (Р°РЅР°Р»РѕРі `Enable Length Snapping`, R-14.1),
    /// РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РІС‹РєР»СЋС‡РµРЅР°.
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

    /// <summary>Р”РµР№СЃС‚РІСѓСЋС‰РёРµ РЅР°СЃС‚СЂРѕР№РєРё РїСЂРёРІСЏР·РѕРє РІРєР»Р°РґРєРё (РїРµСЂРµР¶РёРІР°СЋС‚ СЃРјРµРЅСѓ РІРєР»Р°РґРєРё).</summary>
    public InferenceSettings InferenceSettings { get; private set; } = InferenceSettings.Default;

    /// <summary>
    /// Р’СЂРµРјРµРЅРЅС‹Р№ РїРѕРєР°Р· СЃРєСЂС‹С‚РѕР№ РіРµРѕРјРµС‚СЂРёРё СѓРґРµСЂР¶Р°РЅРёРµРј Ctrl+Shift вЂ” РёРЅРґРёРєР°С‚РѕСЂ
    /// РІ СЃС‚Р°С‚СѓСЃ-СЃС‚СЂРѕРєРµ (R-24).
    /// </summary>
    public bool IsHiddenGeometryPreview
    {
        get => _isHiddenGeometryPreview;
        set => SetField(ref _isHiddenGeometryPreview, value);
    }

    /// <summary>РџРѕР»СЊР·РѕРІР°С‚РµР»СЊ РёР·РјРµРЅРёР» РЅР°СЃС‚СЂРѕР№РєРё РїСЂРёРІСЏР·РѕРє вЂ” РёС… РЅСѓР¶РЅРѕ СЃРѕС…СЂР°РЅРёС‚СЊ.</summary>
    public event Action? InferenceSettingsChanged;

    /// <summary>РџСЂРёРјРµРЅРёС‚СЊ РЅР°СЃС‚СЂРѕР№РєРё РїСЂРёРІСЏР·РѕРє (Р·Р°РіСЂСѓР·РєР° РёР· С„Р°Р№Р»Р°, СЃРјРµРЅР° РІРєР»Р°РґРєРё).</summary>
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

    /// <summary>РћСЂС‚РѕРіРѕРЅР°Р»СЊРЅР°СЏ РїСЂРѕРµРєС†РёСЏ РєР°РјРµСЂС‹ (С‚СѓРјР±Р»РµСЂ С€Р°РїРєРё, РІРёРґР¶РµС‚С‹ РІСЊСЋРїРѕСЂС‚Р°).</summary>
    public bool IsOrthographic
    {
        get => _isOrthographic;
        set => SetField(ref _isOrthographic, value);
    }

    /// <summary>Р РµР¶РёРј РѕС‚РѕР±СЂР°Р¶РµРЅРёСЏ РІСЊСЋРїРѕСЂС‚Р° (РєР°СЂРєР°СЃ/С€С‚СЂРёС…РѕРІРєР°/СЂРµРЅС‚РіРµРЅ).</summary>
    public RenderModeOption SelectedRenderMode
    {
        get => _selectedRenderMode;
        set
        {
            // ComboBox РїСЂРё РїРµСЂРµСЃРѕР·РґР°РЅРёРё РєРѕРЅС‚РµРЅС‚Р° РІРєР»Р°РґРєРё СЃР±СЂР°СЃС‹РІР°РµС‚ SelectedItem
            // РІ null вЂ” С‚Р°РєРѕР№ СЃР±СЂРѕСЃ РЅРµ РґРѕР»Р¶РµРЅ Р·Р°С‚РёСЂР°С‚СЊ С‚РµРєСѓС‰РёР№ СЂРµР¶РёРј: HUD Рё
            // РІСЊСЋРїРѕСЂС‚ С‡РёС‚Р°СЋС‚ Р·РЅР°С‡РµРЅРёРµ Р±РµР· РїСЂРѕРІРµСЂРєРё РЅР° null.
            if (value is null || !SetField(ref _selectedRenderMode, value))
            {
                return;
            }

            InvalidateAll();
        }
    }

    /// <summary>Р•РґРёРЅРёС†С‹ РёР·РјРµСЂРµРЅРёСЏ СЃС†РµРЅС‹ (РјРј/СЃРј/Рј/РґСЋР№РјС‹) вЂ” РїРµСЂРµРєР»СЋС‡Р°СЋС‚СЃСЏ РІ HUD РІСЊСЋРїРѕСЂС‚Р°.</summary>
    public UnitOption SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            // СЃРј. SelectedRenderMode: null РѕС‚ РїСЂРёРІСЏР·РєРё ComboBox РёРіРЅРѕСЂРёСЂСѓРµС‚СЃСЏ,
            // РёРЅР°С‡Рµ HUD РїР°РґР°Р» СЃ NullReferenceException РїСЂРё РїРµСЂРµРєР»СЋС‡РµРЅРёРё РІРєР»Р°РґРѕРє.
            if (value is null || !SetField(ref _selectedUnit, value))
            {
                return;
            }

            Scene.Unit = value.Unit;
            InvalidateAll();
        }
    }

    /// <summary>РљРѕРѕСЂРґРёРЅР°С‚С‹ С‚РѕС‡РєРё РїСЂРёРІСЏР·РєРё РїРѕРґ РєСѓСЂСЃРѕСЂРѕРј РІ РµРґРёРЅРёС†Р°С… СЃС†РµРЅС‹; РїСѓСЃС‚Рѕ вЂ” РїСЂРёРІСЏР·РєРё РЅРµС‚.</summary>
    public string CursorCoordinates
    {
        get => _cursorCoordinates;
        private set => SetField(ref _cursorCoordinates, value);
    }

    public bool HasCursorCoordinates => _cursorCoordinates.Length > 0;

    /// <summary>РћР±РЅРѕРІРёС‚СЊ РєРѕРѕСЂРґРёРЅР°С‚РЅСѓСЋ СЃС‚СЂРѕРєСѓ РїРѕ С‚РѕС‡РєРµ РїСЂРёРІСЏР·РєРё (РјРёСЂРѕРІС‹Рµ РєРѕРѕСЂРґРёРЅР°С‚С‹, РјРј).</summary>
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
