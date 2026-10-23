using SmetaNA.EditorUI.Licensing;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Themes;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Licensing;

namespace SmetaNA.EditorUI;

/// <summary>Состав приложения: сервисы, общие для окна и элементов управления.</summary>
public static class EditorServices
{
    private static readonly object LicenseSync = new();
    private static LicenseRuntime? _licenseRuntime;

    public static ILocalizationService Localization { get; } = new LocalizationService();

    public static ThemeService Theme { get; } = new();

    private sealed record LicenseRuntime(LicenseService Service, LicenseEnvironment.VendorKey VendorKey);

    /// <summary>Сервис лицензирования поверх локального состояния (работает офлайн).</summary>
    public static LicenseService License => Runtime.Service;

    /// <summary>View-модель экрана лицензии для окна.</summary>
    public static LicenseWindowViewModel CreateLicenseViewModel()
    {
        LicenseRuntime runtime = Runtime;
        return new LicenseWindowViewModel(
            Localization,
            runtime.Service,
            runtime.VendorKey.PublicPem,
            runtime.VendorKey.IsDevKey);
    }

    private static LicenseRuntime Runtime
    {
        get
        {
            lock (LicenseSync)
            {
                return _licenseRuntime ??= CreateRuntime();
            }
        }
    }

    private static LicenseRuntime CreateRuntime()
    {
        string stateDirectory = LicenseEnvironment.GetStateDirectory();
        LicenseEnvironment.VendorKey key = LicenseEnvironment.EnsureVendorPublicKeyPem();
        try
        {
            return new LicenseRuntime(new LicenseService(key.PublicPem, stateDirectory), key);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // файл ключа повреждён — откатываемся на временный ключ разработчика
            Console.WriteLine($"[LICENSE] vendor key unusable ({ex.Message}), falling back to dev key");
            key = new LicenseEnvironment.VendorKey(LicenseEnvironment.EnsureDevVendorKeyPem(), IsDevKey: true);
            return new LicenseRuntime(new LicenseService(key.PublicPem, stateDirectory), key);
        }
    }
}
