using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Регенерация голден-фикстур экспорта (Fixtures/*.bin.stl, *.ascii.stl, *.obj):
/// запуск <c>dotnet test</c> с переменной окружения SMETANA_REGEN_FIXTURES=1.
/// Обычный прогон тест ничего не делает; байтовая стабильность проверяется
/// сравнением экспорта с закоммиченными фикстурами в StlExportTests/ObjTests.
/// </summary>
public sealed class FixtureGeneratorTests
{
    [Fact]
    public void RegenerateGoldenFixtures_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("SMETANA_REGEN_FIXTURES") == "1")
        {
            Fixtures.Regenerate();
        }
    }
}
