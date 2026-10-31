using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Tools;
using ProjectDocument = SmetaNA.Formats.ProjectFile.ProjectDocument;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Состояние редактора окна: сцена, история редактирования и контроллер
/// инструментов (контракты SmetaNA.Core используются только для чтения/вызова).
/// </summary>
public sealed class EditorSession
{
    public EditorSession()
    {
        UndoStack undoStack = new();
        Scene = new Scene();
        History = new UndoHistoryService(undoStack);
        Tools = new ToolController(Scene, undoStack);
        Shortcuts = new Commands.ShortcutMap();
    }

    public Scene Scene { get; }

    public UndoHistoryService History { get; }

    /// <summary>Инструменты редактора (маршрутизация ввода вьюпорта).</summary>
    public ToolController Tools { get; }

    public Commands.ShortcutMap Shortcuts { get; }

    /// <summary>
    /// Загрузить документ проекта в сцену сессии: содержимое заменяется,
    /// история сбрасывается (новый файл — новая история), inference-кэш чистится.
    /// </summary>
    public void LoadDocument(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        while (Scene.Root.Children.Count > 0)
        {
            Scene.Root.RemoveChild(Scene.Root.Children[^1]);
        }

        foreach (Tag tag in Scene.Tags.Tags.ToList())
        {
            Scene.Tags.Remove(tag);
        }

        Scene source = document.Scene;
        foreach (Tag tag in source.Tags.Tags.ToList())
        {
            source.Tags.Remove(tag);
            Scene.Tags.Add(tag);
        }

        while (source.Root.Children.Count > 0)
        {
            SceneNode child = source.Root.Children[0];
            source.Root.RemoveChild(child);
            Scene.Root.AddChild(child);
        }

        Scene.Unit = source.Unit;
        Tools.Context.InvalidateInferenceCache();
        History.Clear();
    }
}
