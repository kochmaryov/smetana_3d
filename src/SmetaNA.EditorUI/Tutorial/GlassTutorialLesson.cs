using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Tutorial;

/// <summary>
/// Урок S1 «Стакан за 15 минут» (сценарий спеки §3.1): семь коротких шагов,
/// каждый подсвечивает нужный инструмент на ленте.
/// </summary>
public static class GlassTutorialLesson
{
    /// <summary>Идентификатор урока.</summary>
    public const string LessonId = "s1.glass";

    /// <summary>Создать урок S1.</summary>
    public static TutorialLesson Create() => new(
        LessonId,
        LocalizationKeys.TutorialS1Name,
        LocalizationKeys.TutorialS1Intro,
        LocalizationKeys.TutorialS1Done,
        [
            new TutorialStep(LocalizationKeys.TutorialS1Step1, null),
            new TutorialStep(LocalizationKeys.TutorialS1Step2, ShortcutCatalog.ToolCircle),
            new TutorialStep(LocalizationKeys.TutorialS1Step3, ShortcutCatalog.ToolPushPull),
            new TutorialStep(LocalizationKeys.TutorialS1Step4, ShortcutCatalog.ToolCircle),
            new TutorialStep(LocalizationKeys.TutorialS1Step5, ShortcutCatalog.ToolPushPull),
            new TutorialStep(LocalizationKeys.TutorialS1Step6, ShortcutCatalog.ToolSoften),
            new TutorialStep(LocalizationKeys.TutorialS1Step7, ShortcutCatalog.ToolPaintBucket),
        ]);
}
