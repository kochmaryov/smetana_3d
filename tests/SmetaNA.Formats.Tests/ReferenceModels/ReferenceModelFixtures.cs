using System.Runtime.CompilerServices;
using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats.Tests.ReferenceModels;

/// <summary>
/// Фикстуры эталонных моделей (tests/SmetaNA.Formats.Tests/Fixtures/ReferenceModels):
/// готовые STL/OBJ/3MF для ручного прогона в PrusaSlicer/Cura (задача 4.6)
/// и golden-сверок. Регенерация — <c>dotnet test</c> с переменной окружения
/// SMETANA_REGEN_FIXTURES=1 (как у <see cref="Fixtures"/>).
/// </summary>
internal static class ReferenceModelFixtures
{
    /// <summary>Имена моделей в порядке фикстур.</summary>
    public static readonly IReadOnlyList<string> ModelNames = ["cup", "stand", "enclosure"];

    /// <summary>Форматы экспорта фикстур.</summary>
    public static readonly IReadOnlyList<MeshFileFormat> Formats =
        [MeshFileFormat.BinaryStl, MeshFileFormat.Obj, MeshFileFormat.ThreeMf];

    /// <summary>Модель по имени фикстуры.</summary>
    public static MeshData Build(string modelName) => modelName switch
    {
        "cup" => ReferenceModelFactory.Cup(),
        "stand" => ReferenceModelFactory.Stand(),
        "enclosure" => ReferenceModelFactory.ElectronicsEnclosure(),
        _ => throw new ArgumentOutOfRangeException(nameof(modelName), modelName, "Неизвестная эталонная модель."),
    };

    /// <summary>Расширение файла формата (включая вид STL).</summary>
    public static string ExtensionOf(MeshFileFormat format) => format switch
    {
        MeshFileFormat.BinaryStl => ".bin.stl",
        MeshFileFormat.AsciiStl => ".ascii.stl",
        MeshFileFormat.Obj => ".obj",
        MeshFileFormat.ThreeMf => ".3mf",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Неизвестный формат фикстур."),
    };

    /// <summary>Путь к каталогу фикстур в выходных данных тестов.</summary>
    public static string OutputPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReferenceModels", fileName);

    /// <summary>Путь к исходному каталогу фикстур (Fixtures/ReferenceModels рядом с проектом тестов).</summary>
    public static string SourceDirectory([CallerFilePath] string callerFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerFilePath)!, "..", "Fixtures", "ReferenceModels"));

    /// <summary>Имя файла фикстуры: «модель.формат».</summary>
    public static string FileName(string modelName, MeshFileFormat format) =>
        modelName + ExtensionOf(format);

    /// <summary>Регенерация всех фикстур эталонных моделей.</summary>
    public static void Regenerate([CallerFilePath] string callerFilePath = "")
    {
        string directory = SourceDirectory(callerFilePath);
        Directory.CreateDirectory(directory);
        foreach (string modelName in ModelNames)
        {
            MeshData mesh = Build(modelName);
            foreach (MeshFileFormat format in Formats)
            {
                string path = Path.Combine(directory, FileName(modelName, format));
                File.WriteAllBytes(path, MeshFile.Export(mesh, format));
            }
        }
    }
}
