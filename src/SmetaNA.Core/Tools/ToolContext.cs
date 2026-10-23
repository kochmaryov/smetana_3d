using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools;

/// <summary>
/// Контекст инструмента: сцена, выборка, запросы к InferenceEngine,
/// привязка VCB и стек истории (<see cref="UndoStack"/>). UI обновляет
/// <see cref="Viewport"/> перед маршрутизацией ввода.
/// </summary>
public sealed class ToolContext
{
    public ToolContext(Scene scene, UndoStack undoStack)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(undoStack);
        Scene = scene;
        UndoStack = undoStack;
    }

    public Scene Scene { get; }

    public UndoStack UndoStack { get; }

    /// <summary>Выборка инструмента (объект/грань).</summary>
    public ToolSelection Selection { get; } = new();

    /// <summary>Камера и вьюпорт для лучей/проекций; обновляется UI перед вводом.</summary>
    public InferenceViewport? Viewport { get; set; }

    /// <summary>Решатель привязок; <c>null</c> — <see cref="InferenceEngine.Solve"/>.</summary>
    public Func<SnapRequest, SnapResult>? SnapSolver { get; set; }

    /// <summary>Состояние числового ввода (VCB) активного инструмента.</summary>
    public ToolVcbState VcbState { get; private set; } = ToolVcbState.Inactive;

    /// <summary>Сцена была изменена командой (UI перестраивает рендер-модель).</summary>
    public event Action? SceneEdited;

    /// <summary>Состояние VCB изменилось.</summary>
    public event Action? VcbStateChanged;

    /// <summary>Запрос привязок к InferenceEngine (или подставленному решателю).</summary>
    public SnapResult Snap(SnapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (SnapSolver ?? InferenceEngine.Solve)(request);
    }

    /// <summary>Снимок сцены для запросов привязок.</summary>
    public InferenceScene BuildInferenceScene(bool includeHidden = false)
        => InferenceSceneBuilder.FromScene(Scene, includeHidden);

    /// <summary>Выбрать грань под курсором (пиксели вьюпорта).</summary>
    public bool TryPickFace(Vector2d cursorPx, out ToolFaceHit hit)
    {
        hit = default;
        if (Viewport is not { } viewport)
        {
            return false;
        }

        return SceneFacePicker.TryPick(Scene, viewport.PickRay(cursorPx), out hit);
    }

    /// <summary>Выполнить команду редактирования (в стек истории) и оповестить UI.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        UndoStack.Push(command);
        SceneEdited?.Invoke();
    }

    /// <summary>Опубликовать состояние числового ввода (вызывается инструментами).</summary>
    public void ReportVcb(ToolValueKind kind, double? value, bool isActive)
    {
        ToolVcbState state = new(isActive, kind, value);
        if (VcbState == state)
        {
            return;
        }

        VcbState = state;
        VcbStateChanged?.Invoke();
    }
}
