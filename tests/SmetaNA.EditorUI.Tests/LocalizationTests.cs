using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Vcb;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки DoD §5.7: 100% строк интерфейса на русском и английском.
/// </summary>
public sealed class LocalizationTests
{
    private static readonly string RussianResxPath =
        Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.resx");

    private static readonly string EnglishResxPath =
        Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.en.resx");

    private static Dictionary<string, string> LoadResx(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);

    private static string[] LiteralKeys() =>
        typeof(LocalizationKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void Resx_Locales_HaveIdenticalKeySets()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);

        Assert.NotEmpty(russian);
        Assert.Equal(russian.Keys.OrderBy(key => key), english.Keys.OrderBy(key => key));
    }

    [Fact]
    public void Resx_Values_AreNonEmpty_AndDifferBetweenLocales()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);

        foreach (KeyValuePair<string, string> pair in russian)
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"Пустое значение: {pair.Key}");
            Assert.NotEqual(pair.Value, english[pair.Key]);
        }
    }

    [Fact]
    public void Resx_FormatPlaceholders_MatchBetweenLocales()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);

        foreach (KeyValuePair<string, string> pair in russian)
        {
            Assert.Equal(Placeholders(pair.Value), Placeholders(english[pair.Key]));
        }
    }

    [Fact]
    public void LocalizationKeys_MatchResxExactly()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);
        string[] keys = LiteralKeys();

        Assert.NotEmpty(keys);
        Assert.Equal(russian.Keys.OrderBy(key => key), keys.OrderBy(key => key));
        Assert.Equal(english.Keys.OrderBy(key => key), keys.OrderBy(key => key));
    }

    [Fact]
    public void LocalizedStrings_ExposesNonEmptyValue_ForEveryPropertyInBothLocales()
    {
        var service = new LocalizationService();
        var strings = new LocalizedStrings(service);
        PropertyInfo[] properties = typeof(LocalizedStrings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
            .ToArray();

        Assert.NotEmpty(properties);
        foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
        {
            service.SetCulture(culture);
            foreach (PropertyInfo property in properties)
            {
                string value = (string)property.GetValue(strings)!;
                Assert.False(string.IsNullOrWhiteSpace(value), $"{property.Name}: пусто для {culture}");
                Assert.NotEqual(property.Name, value);
            }
        }
    }

    [Fact]
    public void LocalizedStrings_RefreshesBindings_OnCultureChange()
    {
        var service = new LocalizationService();
        var strings = new LocalizedStrings(service);
        int refreshes = 0;
        strings.PropertyChanged += (_, _) => refreshes++;

        service.SetCulture(EditorCulture.English);

        Assert.Equal(1, refreshes);
    }

    [Fact]
    public void Service_ReturnsLocalizedValues_AndFallsBackToKey()
    {
        var service = new LocalizationService();

        Assert.Equal(EditorCulture.Russian, service.Culture);
        string russian = service.Get(LocalizationKeys.AppTitle);

        service.SetCulture(EditorCulture.English);
        string english = service.Get(LocalizationKeys.AppTitle);

        Assert.NotEqual(russian, english);
        Assert.Equal(english, service.Get(LocalizationKeys.AppTitle, EditorCulture.English));
        Assert.Equal("нет.ключа", service.Get("нет.ключа"));
    }

    [Fact]
    public void Service_SameCultureChange_DoesNotRaiseEvent()
    {
        var service = new LocalizationService();
        int raised = 0;
        service.CultureChanged += () => raised++;

        service.SetCulture(EditorCulture.Russian);
        Assert.Equal(0, raised);

        service.SetCulture(EditorCulture.English);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void EveryVcbError_HasLocalizedMessage()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);

        foreach (VcbError error in Enum.GetValues<VcbError>())
        {
            if (error == VcbError.None)
            {
                continue;
            }

            string key = LocalizationKeys.ForVcbError(error);
            Assert.Contains(key, russian.Keys);
            Assert.Contains(key, english.Keys);
        }
    }

    [Fact]
    public void EveryUnitAndPrompt_HasLocalizedMessage()
    {
        Dictionary<string, string> russian = LoadResx(RussianResxPath);
        Dictionary<string, string> english = LoadResx(EnglishResxPath);

        foreach (LengthUnit unit in Enum.GetValues<LengthUnit>())
        {
            Assert.Contains(LocalizationKeys.ForUnit(unit), russian.Keys);
            Assert.Contains(LocalizationKeys.ForUnit(unit), english.Keys);
        }

        foreach (VcbInputKind kind in Enum.GetValues<VcbInputKind>())
        {
            Assert.Contains(LocalizationKeys.ForVcbPrompt(kind), russian.Keys);
            Assert.Contains(LocalizationKeys.ForVcbPrompt(kind), english.Keys);
        }
    }

    [Fact]
    public void Axaml_ContainsNoHardcodedUserStrings()
    {
        string sourceRoot = Path.Combine(AppContext.BaseDirectory, "Source");
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

    private static HashSet<string> Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+\}", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
}
