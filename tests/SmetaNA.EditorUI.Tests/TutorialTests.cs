using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.Tutorial;
using SmetaNA.EditorUI.ViewModels;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки режима «Новичок» и урока S1 (задача 6.2): шаги локализованы,
/// подсветка инструментов и навигация работают корректно.
/// </summary>
public sealed class TutorialTests
{
    [Fact]
    public void GlassLesson_HasSevenSteps_WithLocalizedTexts()
    {
        TutorialLesson lesson = GlassTutorialLesson.Create();
        var service = new LocalizationService();

        Assert.Equal(GlassTutorialLesson.LessonId, lesson.Id);
        Assert.Equal(7, lesson.Steps.Count);
        foreach (EditorCulture culture in new[] { EditorCulture.Russian, EditorCulture.English })
        {
            service.SetCulture(culture);
            foreach (string key in new[] { lesson.NameKey, lesson.IntroKey, lesson.DoneKey }
                .Concat(lesson.Steps.Select(step => step.TextKey)))
            {
                string text = service.Get(key);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{key}: пусто для {culture}");
                Assert.NotEqual(key, text);
            }
        }
    }

    [Fact]
    public void GlassLesson_HighlightsRealRibbonTools()
    {
        TutorialLesson lesson = GlassTutorialLesson.Create();
        var knownCommands = new HashSet<string>(
            ShortcutCatalog.Defaults.Select(definition => definition.CommandId),
            StringComparer.Ordinal);

        foreach (TutorialStep step in lesson.Steps)
        {
            if (step.CommandId is { } commandId)
            {
                Assert.Contains(commandId, knownCommands);
            }
        }

        // хотя бы один шаг подсвечивает инструмент
        Assert.Contains(lesson.Steps, step => step.CommandId is not null);
    }

    [Fact]
    public void Navigation_WalksAllSteps_ThenCompletes()
    {
        TutorialViewModel tutorial = CreateTutorial();
        Assert.False(tutorial.IsActive);
        Assert.False(tutorial.NextCommand.CanExecute(null));

        tutorial.Start();
        Assert.True(tutorial.IsActive);
        Assert.Equal(0, tutorial.CurrentStepIndex);
        Assert.Null(tutorial.HighlightedCommandId); // первый шаг — осмотр вьюпорта
        Assert.False(tutorial.BackCommand.CanExecute(null));

        tutorial.Next();
        Assert.Equal(1, tutorial.CurrentStepIndex);
        Assert.Equal(ShortcutCatalog.ToolCircle, tutorial.HighlightedCommandId);
        Assert.True(tutorial.BackCommand.CanExecute(null));

        for (int i = 0; i < tutorial.StepCount - 1; i++)
        {
            tutorial.Next();
        }

        Assert.True(tutorial.IsCompleted);
        Assert.False(tutorial.NextCommand.CanExecute(null));
        Assert.Null(tutorial.HighlightedCommandId);
    }

    [Fact]
    public void Back_FromCompletion_ReturnsToLastStep()
    {
        TutorialViewModel tutorial = CreateTutorial();
        tutorial.Start();
        while (tutorial.NextCommand.CanExecute(null))
        {
            tutorial.Next();
        }

        Assert.True(tutorial.IsCompleted);
        tutorial.Back();
        Assert.False(tutorial.IsCompleted);
        Assert.Equal(tutorial.StepCount - 1, tutorial.CurrentStepIndex);
    }

    [Fact]
    public void Restart_ResetsProgress_CloseClearsHighlight()
    {
        TutorialViewModel tutorial = CreateTutorial();
        tutorial.Start();
        tutorial.Next();
        Assert.NotNull(tutorial.HighlightedCommandId);

        tutorial.Restart();
        Assert.Equal(0, tutorial.CurrentStepIndex);
        Assert.False(tutorial.IsCompleted);

        tutorial.Close();
        Assert.False(tutorial.IsActive);
        Assert.Null(tutorial.HighlightedCommandId);
        Assert.False(tutorial.RestartCommand.CanExecute(null));
    }

    [Fact]
    public void ProgressText_ContainsStepNumbers_InBothCultures()
    {
        var service = new LocalizationService();
        TutorialViewModel tutorial = new(service, GlassTutorialLesson.Create());
        tutorial.Start();

        service.SetCulture(EditorCulture.Russian);
        Assert.Contains("1", tutorial.ProgressText, StringComparison.Ordinal);
        Assert.Contains("7", tutorial.ProgressText, StringComparison.Ordinal);

        service.SetCulture(EditorCulture.English);
        Assert.Contains("1", tutorial.ProgressText, StringComparison.Ordinal);
        Assert.Contains("7", tutorial.ProgressText, StringComparison.Ordinal);
    }

    [Fact]
    public void Lesson_RequiresAtLeastOneStep()
    {
        var empty = new TutorialLesson("x", "a", "b", "c", []);
        Assert.Throws<ArgumentException>(() => new TutorialViewModel(new LocalizationService(), empty));
    }

    private static TutorialViewModel CreateTutorial() =>
        new(new LocalizationService(), GlassTutorialLesson.Create());
}
