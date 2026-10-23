using System.Collections.ObjectModel;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Rendering.PrintVolume;

namespace SmetaNA.EditorUI.PrintVolume;

/// <summary>
/// View-модель панели зоны печати (задача 4.5): выбор профиля принтера,
/// свойства профиля (габариты, сопло, высота слоя) и переключатель показа
/// зоны в вьюпорте. Выбранный профиль публикуется в
/// <see cref="PrintVolumeState"/> — его читают GL-вьюпорты.
/// </summary>
public sealed class PrintVolumeViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private PrinterProfileOption? _selectedProfile;
    private bool _isVisible;

    public PrintVolumeViewModel(ILocalizationService? localization = null)
    {
        _localization = localization ?? EditorServices.Localization;

        Profiles = [];
        foreach (PrinterProfile profile in PrinterProfileCatalog.All)
        {
            Profiles.Add(new PrinterProfileOption(profile, _localization.Culture));
        }

        _selectedProfile = Profiles[0];
        _isVisible = false;
        _localization.CultureChanged += OnCultureChanged;
        ApplyState();
    }

    /// <summary>Профили принтеров для списка выбора.</summary>
    public ObservableCollection<PrinterProfileOption> Profiles { get; }

    /// <summary>Выбранный профиль принтера.</summary>
    public PrinterProfileOption? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetField(ref _selectedProfile, value))
            {
                return;
            }

            ApplyState();
            OnPropertyChanged(nameof(ProfileName));
            OnPropertyChanged(nameof(VolumeText));
            OnPropertyChanged(nameof(NozzleText));
            OnPropertyChanged(nameof(LayerText));
            OnPropertyChanged(nameof(NotesText));
        }
    }

    /// <summary>Показывать зону печати в вьюпорте.</summary>
    public bool IsVolumeVisible
    {
        get => _isVisible;
        set
        {
            if (SetField(ref _isVisible, value))
            {
                ApplyState();
            }
        }
    }

    /// <summary>Заголовок панели.</summary>
    public string Title => PrintVolumeStrings.Get(PrintVolumeStrings.Title, _localization.Culture);

    /// <summary>Подпись выбора профиля.</summary>
    public string ProfileLabel => PrintVolumeStrings.Get(PrintVolumeStrings.ProfileLabel, _localization.Culture);

    /// <summary>Подпись переключателя показа зоны.</summary>
    public string ShowVolumeLabel => PrintVolumeStrings.Get(PrintVolumeStrings.ShowVolume, _localization.Culture);

    /// <summary>Подпись габаритов.</summary>
    public string VolumeLabel => PrintVolumeStrings.Get(PrintVolumeStrings.VolumeLabel, _localization.Culture);

    /// <summary>Подпись сопла.</summary>
    public string NozzleLabel => PrintVolumeStrings.Get(PrintVolumeStrings.NozzleLabel, _localization.Culture);

    /// <summary>Подпись высоты слоя.</summary>
    public string LayerLabel => PrintVolumeStrings.Get(PrintVolumeStrings.LayerLabel, _localization.Culture);

    /// <summary>Подпись примечания.</summary>
    public string NotesLabel => PrintVolumeStrings.Get(PrintVolumeStrings.NotesLabel, _localization.Culture);

    /// <summary>Имя выбранного профиля.</summary>
    public string ProfileName => _selectedProfile?.Name ?? string.Empty;

    /// <summary>Габариты зоны печати выбранного профиля.</summary>
    public string VolumeText => _selectedProfile is null ? string.Empty : _selectedProfile.Summary;

    /// <summary>Размер сопла выбранного профиля.</summary>
    public string NozzleText => _selectedProfile is null
        ? string.Empty
        : FormatMeasure(_selectedProfile.Profile.NozzleDiameterMm);

    /// <summary>Диапазон высот слоя выбранного профиля.</summary>
    public string LayerText
    {
        get
        {
            if (_selectedProfile is not { } option)
            {
                return string.Empty;
            }

            PrinterProfile profile = option.Profile;
            return $"{FormatMeasure(profile.LayerHeightMinMm)} – {FormatMeasure(profile.LayerHeightMaxMm)}";
        }
    }

    /// <summary>Примечание выбранного профиля.</summary>
    public string NotesText => _selectedProfile?.Profile.Notes ?? string.Empty;

    private string FormatMeasure(double valueMm) =>
        $"{PrintVolumeStrings.FormatNumber(valueMm, _localization.Culture)} {PrintVolumeStrings.Get(PrintVolumeStrings.MillimeterUnit, _localization.Culture)}";

    private void ApplyState() =>
        PrintVolumeState.Current = _isVisible ? _selectedProfile?.Profile : null;

    private void OnCultureChanged()
    {
        foreach (PrinterProfileOption option in Profiles)
        {
            option.SetCulture(_localization.Culture);
        }

        InvalidateAll();
    }
}
