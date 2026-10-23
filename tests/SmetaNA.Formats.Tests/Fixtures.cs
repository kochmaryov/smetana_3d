using System.Runtime.CompilerServices;
using SmetaNA.Formats.Obj;
using SmetaNA.Formats.Stl;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Доступ к файлам фикстур (tests/SmetaNA.Formats.Tests/Fixtures).
/// Голден-файлы байт-стабильного экспорта регенерируются командой
/// <c>dotnet test</c> с переменной окружения SMETANA_REGEN_FIXTURES=1;
/// после регенерации изменения фикстур должны быть осознанно закоммичены.
/// </summary>
internal static class Fixtures
{
    /// <summary>Чтение фикстуры из каталога выходных данных тестов.</summary>
    public static byte[] Read(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>Путь к исходному каталогу фикстур (каталог этого файла в репозитории).</summary>
    public static string SourceDirectory([CallerFilePath] string callerFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(callerFilePath)!, "Fixtures");

    /// <summary>Регенерация голден-фикстур экспорта (вызывается при SMETANA_REGEN_FIXTURES=1).</summary>
    public static void Regenerate([CallerFilePath] string callerFilePath = "")
    {
        string directory = SourceDirectory(callerFilePath);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "cube.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Cube()));
        File.WriteAllBytes(Path.Combine(directory, "cube.ascii.stl"), StlExporter.ExportAscii(GoldenMeshes.Cube()));
        File.WriteAllBytes(Path.Combine(directory, "cube.obj"), ObjExporter.Export(GoldenMeshes.Cube()));
        File.WriteAllBytes(Path.Combine(directory, "cup.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Cup()));
        File.WriteAllBytes(Path.Combine(directory, "stand.bin.stl"), StlExporter.ExportBinary(GoldenMeshes.Stand()));
    }
}
