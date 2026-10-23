namespace SmetaNA.EditorUI.Tutorial;

/// <summary>
/// Интерактивный урок: сценарий из шагов с подсветкой инструментов (M10, задача 6.2).
/// </summary>
/// <param name="Id">Идентификатор урока.</param>
/// <param name="NameKey">Ключ названия урока.</param>
/// <param name="IntroKey">Ключ вступления.</param>
/// <param name="DoneKey">Ключ поздравления по завершении.</param>
/// <param name="Steps">Шаги урока.</param>
public sealed record TutorialLesson(
    string Id,
    string NameKey,
    string IntroKey,
    string DoneKey,
    IReadOnlyList<TutorialStep> Steps);
