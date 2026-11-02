using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;
using SmetaNA.EditorUI.Tools;

namespace SmetaNA.EditorUI.Sessions;

/// <summary>
/// Документ проекта (одна вкладка): собственная сцена с единицами измерения,
/// собственная история редактирования и собственный контроллер инструментов.
/// Сессия держит активный документ, поэтому вкладки не делят содержимое.
/// </summary>
public sealed class EditorDocument
{
    public EditorDocument()
        : this(new Scene())
    {
    }

    /// <summary>Документ поверх готовой сцены (открытый файл, эталонная сцена смоука).</summary>
    public EditorDocument(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        Scene = scene;
        UndoStack undoStack = new();
        History = new UndoHistoryService(undoStack);
        Tools = new ToolController(Scene, undoStack);
    }

    /// <summary>Сцена документа (единицы измерения хранятся в ней же).</summary>
    public Scene Scene { get; }

    /// <summary>История undo/redo документа.</summary>
    public UndoHistoryService History { get; }

    /// <summary>Инструменты документа: ввод вьюпорта идёт в активный документ.</summary>
    public ToolController Tools { get; }
}
