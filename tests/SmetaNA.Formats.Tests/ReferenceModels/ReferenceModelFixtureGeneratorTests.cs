using Xunit;

namespace SmetaNA.Formats.Tests.ReferenceModels;

/// <summary>
/// Регенерация фикстур эталонных моделей (Fixtures/ReferenceModels/*):
/// запуск <c>dotnet test</c> с переменной окружения SMETANA_REGEN_FIXTURES=1.
/// Обычный прогон тест ничего не делает; содержимое закоммиченных файлов
/// сверяется с фабрикой моделей в <see cref="ReferenceModelExportTests"/>.
/// </summary>
public sealed class ReferenceModelFixtureGeneratorTests
{
    [Fact]
    public void RegenerateReferenceModelFixtures_WhenRequested()
    {
        if (Environment.GetEnvironmentVariable("SMETANA_REGEN_FIXTURES") == "1")
        {
            ReferenceModelFixtures.Regenerate();
        }
    }
}
