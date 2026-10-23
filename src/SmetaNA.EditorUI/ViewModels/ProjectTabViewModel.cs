using System.Collections.ObjectModel;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;
using SmetaNA.Rendering;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Вкладка проекта: сцена, единицы и строки для вьюпорта (заготовка — без документов).</summary>
public sealed class ProjectTabViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private UnitOption _selectedUnit;
    private RenderModeOption _selectedRenderMode;

    public ProjectTabViewModel(ILocalizationService localization, Scene scene, int number)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(scene);
        _localization = localization;
        Scene = scene;
        Number = number;

        Strings = new LocalizedStrings(localization);
        Units = new ObservableCollection<UnitOption>(
            Enum.GetValues<LengthUnit>().Select(unit => new UnitOption(localization, unit)));
        _selectedUnit = Units.First(option => option.Unit == scene.Unit);

        RenderModes = new ObservableCollection<RenderModeOption>(
            Enum.GetValues<RenderMode>().Select(mode => new RenderModeOption(localization, mode)));
        _selectedRenderMode = RenderModes.First(option => option.Mode == RenderMode.Shaded);

        _localization.CultureChanged += InvalidateAll;
    }

    public int Number { get; }

    /// <summary>Сцена вкладки (контракты SmetaNA.Core используются только для чтения/вызова).</summary>
    public Scene Scene { get; }

    public LocalizedStrings Strings { get; }

    public ObservableCollection<UnitOption> Units { get; }

    public ObservableCollection<RenderModeOption> RenderModes { get; }

    /// <summary>Режим отображения вьюпорта (каркас/штриховка/рентген).</summary>
    public RenderModeOption SelectedRenderMode
    {
        get => _selectedRenderMode;
        set
        {
            if (SetField(ref _selectedRenderMode, value) && value is not null)
            {
                InvalidateAll();
            }
        }
    }

    /// <summary>Единицы измерения сцены (мм/см/м/дюймы) — переключаются в HUD вьюпорта.</summary>
    public UnitOption SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (!SetField(ref _selectedUnit, value) || value is null)
            {
                return;
            }

            Scene.Unit = value.Unit;
            InvalidateAll();
        }
    }

    public string Title =>
        string.Format(_localization.Get(LocalizationKeys.ProjectName), Number);
}
