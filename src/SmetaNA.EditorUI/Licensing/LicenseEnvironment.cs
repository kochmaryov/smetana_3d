using System.Security.Cryptography;
using SmetaNA.Licensing;

namespace SmetaNA.EditorUI.Licensing;

/// <summary>
/// Окружение лицензирования: локальное хранилище состояния и открытый ключ
/// продавца. Работает полностью офлайн (DoD §5.8): ключ берётся из переменной
/// окружения или файла; при отсутствии генерируется временный ключ разработчика
/// (перед релизом подменяется настоящим ключом продавца).
/// </summary>
public static class LicenseEnvironment
{
    /// <summary>Переменная окружения с PEM-текстом или путём к файлу ключа продавца.</summary>
    public const string VendorPublicKeyEnvironmentVariable = "SMETANA_VENDOR_PUBLIC_KEY";

    private const string VendorPublicKeyFileName = "vendor-public.pem";
    private const string DevVendorPrivateKeyFileName = "dev-vendor-private.pem";

    /// <summary>Каталог локального состояния лицензии (создаётся при необходимости).</summary>
    public static string GetStateDirectory()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string path = Path.Combine(root, "SmetaNA", "license");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Загруженный открытый ключ продавца (PEM) и признак временного ключа.</summary>
    public sealed record VendorKey(string PublicPem, bool IsDevKey);

    /// <summary>
    /// Открытый ключ продавца: переменная окружения, файл рядом с приложением
    /// или в каталоге состояния; при отсутствии — временный ключ разработчика.
    /// </summary>
    public static VendorKey EnsureVendorPublicKeyPem()
    {
        if (TryLoadVendorPublicKeyPem(out string pem, out bool isDevKey))
        {
            return new VendorKey(pem, isDevKey);
        }

        return new VendorKey(EnsureDevVendorKeyPem(), IsDevKey: true);
    }

    /// <summary>Поиск ключа продавца без генерации; <c>false</c> — ключ не настроен.</summary>
    public static bool TryLoadVendorPublicKeyPem(out string publicPem) =>
        TryLoadVendorPublicKeyPem(out publicPem, out _);

    /// <summary>
    /// Поиск ключа продавца: переменная окружения, файл рядом с приложением или в
    /// каталоге состояния. <paramref name="isDevKey"/> — ключ сгенерирован разработчиком.
    /// </summary>
    public static bool TryLoadVendorPublicKeyPem(out string publicPem, out bool isDevKey)
    {
        string? candidate = Environment.GetEnvironmentVariable(VendorPublicKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            if (File.Exists(candidate))
            {
                publicPem = File.ReadAllText(candidate).Trim();
                isDevKey = false;
                return publicPem.Length > 0;
            }

            if (candidate.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal))
            {
                publicPem = candidate.Trim();
                isDevKey = false;
                return true;
            }
        }

        string appFile = Path.Combine(AppContext.BaseDirectory, VendorPublicKeyFileName);
        if (File.Exists(appFile))
        {
            string text = File.ReadAllText(appFile).Trim();
            if (text.Length > 0)
            {
                publicPem = text;
                isDevKey = false;
                return true;
            }
        }

        string stateDirectory = GetStateDirectory();
        string stateFile = Path.Combine(stateDirectory, VendorPublicKeyFileName);
        if (File.Exists(stateFile))
        {
            string text = File.ReadAllText(stateFile).Trim();
            if (text.Length > 0)
            {
                publicPem = text;
                isDevKey = File.Exists(Path.Combine(stateDirectory, DevVendorPrivateKeyFileName));
                return true;
            }
        }

        publicPem = string.Empty;
        isDevKey = false;
        return false;
    }

    /// <summary>
    /// Временный ключ разработчика в каталоге состояния: пара генерируется один раз
    /// на машине, закрытый ключ сохраняется для выпуска тестовых лицензий офлайн.
    /// </summary>
    public static string EnsureDevVendorKeyPem()
    {
        string directory = GetStateDirectory();
        string publicFile = Path.Combine(directory, VendorPublicKeyFileName);
        string privateFile = Path.Combine(directory, DevVendorPrivateKeyFileName);

        if (File.Exists(publicFile) && File.Exists(privateFile))
        {
            return File.ReadAllText(publicFile).Trim();
        }

        using ECDsa signingKey = LicenseKeyGenerator.CreateSigningKey();
        using var generator = new LicenseKeyGenerator(signingKey);
        string publicPem = generator.ExportPublicKeyPem();
        File.WriteAllText(publicFile, publicPem);
        File.WriteAllText(privateFile, generator.ExportPrivateKeyPem());

        Console.WriteLine($"[LICENSE] dev vendor key generated: {publicFile}");
        return publicPem;
    }
}
