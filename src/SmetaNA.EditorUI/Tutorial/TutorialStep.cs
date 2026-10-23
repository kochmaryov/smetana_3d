namespace SmetaNA.EditorUI.Tutorial;

/// <summary>
/// Шаг интерактивного урока: текст из общего каталога локализации и
/// инструмент, кнопка которого подсвечивается на ленте.
/// </summary>
/// <param name="TextKey">Ключ текста шага.</param>
/// <param name="CommandId">Идентификатор команды для подсветки (или <c>null</c>).</param>
public sealed record TutorialStep(string TextKey, string? CommandId);
