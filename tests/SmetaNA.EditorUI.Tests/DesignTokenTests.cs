using System.Text.RegularExpressions;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Аудит дизайн-токенов (спека ui-redesign §4 «Визуальная система», риск R5):
/// литералы цветов разрешены только в словаре токенов — весь остальной XAML
/// обязан ссылаться на токены через DynamicResource, иначе светлая тема
/// «производная от токенов» разваливается на захардкоженных экранах.
/// </summary>
public sealed class DesignTokenTests
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
    public void ColorLiterals_ExistOnlyInDesignTokens()
    {
        string sourceRoot = FindEditorUiSourceDirectory();
        string[] axamlFiles = Directory.GetFiles(sourceRoot, "*.axaml", SearchOption.AllDirectories);
        Assert.NotEmpty(axamlFiles);

        // Захватывает Background/Foreground/BorderBrush/Fill/Stroke/Color="#..."
        var colorPattern = new Regex(
            "(?:Background|Foreground|BorderBrush|Fill|Stroke|Color)\\s*=\\s*\"#",
            RegexOptions.CultureInvariant);

        foreach (string file in axamlFiles)
        {
            if (string.Equals(Path.GetFileName(file), "DesignTokens.axaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string content = File.ReadAllText(file);
            foreach (Match match in colorPattern.Matches(content))
            {
                Assert.Fail(
                    $"{Path.GetFileName(file)}: литерал цвета вместо токена: {match.Value.Trim()} " +
                    "(цвета — только в Themes/DesignTokens.axaml)");
            }
        }
    }

    [Fact]
    public void ThemeBrushTokens_ExistInBothVariants()
    {
        string sourceRoot = FindEditorUiSourceDirectory();
        string tokensPath = Path.Combine(sourceRoot, "Themes", "DesignTokens.axaml");
        Assert.True(File.Exists(tokensPath), "Отсутствует Themes/DesignTokens.axaml");

        string content = File.ReadAllText(tokensPath);
        string[] requiredTokens =
        [
            "ThemeBackgroundBrush",
            "ThemeControlBackground",
            "ThemeBorderMidBrush",
            "ThemeForegroundBrush",
            "ThemeAccentBrush",
        ];

        // Каждый обязательный токен должен быть определён в обеих темах (2 раза).
        foreach (string token in requiredTokens)
        {
            int count = Regex.Matches(content, $"x:Key=\"{token}\"").Count;
            Assert.True(count >= 2, $"Токен {token} должен быть определён и в Dark, и в Light");
        }
    }
}
