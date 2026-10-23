using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SmetaNA.Formats.ThreeMf;

/// <summary>
/// Загрузка нативной библиотеки lib3mf для официального биндинга (ThreeMf/Lib3MF).
/// Биндинг обращается к «lib3mf.dll» (имя Windows); резолвер подставляет имя текущей
/// платформы и ищет бинарник рядом со сборкой и в runtimes/&lt;rid&gt;/native/ —
/// в такой раскладке библиотека поставляется в продакшене (Windows и Linux).
/// Бинарники не коммитятся: их воспроизводит ThreeMf/native/fetch-lib3mf.ps1
/// (lib3mf 2.5.0, BSD-2-Clause, официальный релиз 3MF Consortium).
/// </summary>
internal static class Lib3mfNative
{
    private const string BindingLibraryName = "lib3mf.dll";
    private static int _resolverRegistered;

    /// <summary>Регистрация резолвера до первого обращения к биндингу.</summary>
#pragma warning disable CA2255 // Резолвер загрузки нативной библиотеки — безобидная инициализация, влияющая только на поиск lib3mf.
    [ModuleInitializer]
    internal static void RegisterResolver()
    {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) != 0)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(Lib3mfNative).Assembly, Resolve);
    }
#pragma warning restore CA2255

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(BindingLibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in CandidatePaths())
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return handle;
            }
        }

        throw new DllNotFoundException(
            "Не найдена нативная библиотека lib3mf (формат 3MF): ожидался файл " + LibraryFileName() +
            " рядом со сборкой или в runtimes/" + RuntimeId() + "/native/. " +
            "Загрузка: powershell -ExecutionPolicy Bypass -File src\\SmetaNA.Formats\\ThreeMf\\native\\fetch-lib3mf.ps1.");
    }

    private static IEnumerable<string> CandidatePaths()
    {
        string fileName = LibraryFileName();
        var directories = new List<string>();
        string baseDirectory = AppContext.BaseDirectory;
        directories.Add(baseDirectory);
        directories.Add(Path.Combine(baseDirectory, "runtimes", RuntimeId(), "native"));

        string? assemblyDirectory = Path.GetDirectoryName(typeof(Lib3mfNative).Assembly.Location);
        if (assemblyDirectory is not null && !assemblyDirectory.Equals(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            directories.Add(assemblyDirectory);
            directories.Add(Path.Combine(assemblyDirectory, "runtimes", RuntimeId(), "native"));
        }

        foreach (string directory in directories)
        {
            yield return Path.Combine(directory, fileName);
        }
    }

    private static string LibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "lib3mf.dll";
        }

        return OperatingSystem.IsMacOS() ? "lib3mf.dylib" : "lib3mf.so";
    }

    private static string RuntimeId()
    {
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64",
        };

        return os + "-" + architecture;
    }
}
