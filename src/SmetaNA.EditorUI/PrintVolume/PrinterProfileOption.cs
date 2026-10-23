using System.ComponentModel;
using SmetaNA.EditorUI.Localization;
using SmetaNA.Rendering.PrintVolume;

namespace SmetaNA.EditorUI.PrintVolume;

/// <summary>
/// Элемент списка профилей принтера в UI: имя модели и краткая строка габаритов
/// («250 × 210 × 210 мм»). Имена производителей/моделей не локализуются,
/// локализуется только единица измерения; смена языка обновляет подписи.
/// </summary>
public sealed class PrinterProfileOption : INotifyPropertyChanged
{
    private EditorCulture _culture;

    public PrinterProfileOption(PrinterProfile profile, EditorCulture culture)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
        _culture = culture;
    }

    /// <summary>Профиль принтера.</summary>
    public PrinterProfile Profile { get; }

    /// <summary>Язык подписей.</summary>
    public EditorCulture Culture => _culture;

    /// <summary>Имя профиля «Производитель Модель».</summary>
    public string Name => Profile.DisplayName;

    /// <summary>Габариты зоны одной строкой.</summary>
    public string Summary
    {
        get
        {
            string unit = PrintVolumeStrings.Get(PrintVolumeStrings.MillimeterUnit, _culture);
            return $"{Profile.BuildVolumeMm.X:0} × {Profile.BuildVolumeMm.Y:0} × {Profile.BuildVolumeMm.Z:0} {unit}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Сменить язык подписей (обновляет привязки).</summary>
    public void SetCulture(EditorCulture culture)
    {
        if (_culture == culture)
        {
            return;
        }

        _culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    public override string ToString() => Name;
}
