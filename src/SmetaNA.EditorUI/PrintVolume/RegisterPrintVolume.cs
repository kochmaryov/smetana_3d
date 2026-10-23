using SmetaNA.Rendering.PrintVolume;

namespace SmetaNA.EditorUI.PrintVolume;

/// <summary>
/// Модуль интеграции зоны печати (задача 4.5) в UI: профили принтеров,
/// view-модель панели и публикация выбранного профиля вьюпортам.
/// <para>
/// Подключение координатором (существующие файлы меняются только точечно):
/// <code>
/// PrintVolumeViewModel viewModel = RegisterPrintVolume.CreateViewModel();
/// // DataContext панели PrintVolumePanel; панель добавляется в MainWindow.axaml
/// </code>
/// Показ зоны в вьюпорте идёт через <see cref="PrintVolumeState"/> —
/// GL-вьюпорт читает профиль каждый кадр, поэтому вкладки и новые вьюпорты
/// подхватывают выбор без дополнительных подключений.
/// </para>
/// </summary>
public static class RegisterPrintVolume
{
    /// <summary>Все профили принтеров (порядок списка в UI).</summary>
    public static IReadOnlyList<PrinterProfile> Profiles => PrinterProfileCatalog.All;

    /// <summary>Новая view-модель панели зоны печати.</summary>
    public static PrintVolumeViewModel CreateViewModel() => new();

    /// <summary>
    /// Применить профиль ко всем вьюпортам (<c>null</c> — выключить зону печати).
    /// </summary>
    public static void Apply(PrinterProfile? profile) => PrintVolumeState.Current = profile;

    /// <summary>Выключить зону печати во вьюпортах.</summary>
    public static void Clear() => PrintVolumeState.Current = null;
}
