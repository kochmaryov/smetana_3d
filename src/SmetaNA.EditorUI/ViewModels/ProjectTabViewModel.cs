using System.Collections.ObjectModel;
using SmetaNA.Core.Model;
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
    private bool _isOrthographic;

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

    public string Title =>
        string.Format(_localization.Get(LocalizationKeys.ProjectName), Number);
}
