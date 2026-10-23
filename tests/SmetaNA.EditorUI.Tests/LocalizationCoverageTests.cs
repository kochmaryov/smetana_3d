using System.Text.RegularExpressions;
using System.Xml.Linq;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Tutorial;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Аудит локализации (DoD §5.7): ни одной строки интерфейса в разметке
/// не задано текстом — все привязаны к каталогу строк; ключи урока и лицензии
/// разрешаются в обоих языках.
/// </summary>
public sealed class LocalizationCoverageTests
{
    private static string FindEditorUiSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "SmetaNA.EditorUI");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден каталог src/SmetaNA.EditorUI рядом с тестами.");
    }

    [Fact]
    public void AllAxamlFiles_HaveNoHardcodedUserStrings()
    {
        string sourceRoot = FindEditorUiSourceDirectory();
        string[] axamlFiles = Directory.GetFiles(sourceRoot, "*.axaml", SearchOption.AllDirectories);
        Assert.NotEmpty(axamlFiles);

        var attributePattern = new Regex(
            "(?<![\\w:.])(Text|Header|Content|PlaceholderText|ToolTip\\.Tip|Title)\\s*=\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.CultureInvariant);

        foreach (string file in axamlFiles)
        {
            string content = File.ReadAllText(file);
            foreach (Match match in attributePattern.Matches(content))
            {
                string value = match.Groups["value"].Value;
                Assert.True(
                    value.StartsWith('{'),
                    $"{Path.GetFileName(file)}: строка задана текстом вместо привязки к ресурсам: {match.Value}");
            }
        }
    }

    [Fact]
    public void TutorialAndLicenseKeys_ResolveInBothCultures()
    {
        var service = new LocalizationService();
        TutorialLesson lesson = GlassTutorialLesson.Create();
        string[] keys =
        [
            lesson.NameKey,
            lesson.IntroKey,
            lesson.DoneKey,
            .. lesson.Steps.Select(step => step.TextKey),
        ];

        foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
        {
            service.SetCulture(culture);
            foreach (string key in keys)
            {
                Assert.NotEqual(key, service.Get(key));
            }
        }
    }

    [Fact]
    public void Resx_SourceFiles_HaveIdenticalKeySets()
    {
        string sourceRoot = FindEditorUiSourceDirectory();
        Dictionary<string, string> russian = LoadResx(Path.Combine(sourceRoot, "Resources", "Strings.resx"));
        Dictionary<string, string> english = LoadResx(Path.Combine(sourceRoot, "Resources", "Strings.en.resx"));

        Assert.Equal(russian.Keys.OrderBy(key => key, StringComparer.Ordinal), english.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> LoadResx(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
}
