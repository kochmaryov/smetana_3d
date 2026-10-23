using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Core.Undo;

namespace SmetaNA.EditorUI.Tools;

/// <summary>
/// Контроллер инструментов UI-слоя: владеет <see cref="ToolContext"/> сцены,
/// маршрутизирует ввод вьюпорта в активный инструмент и публикует его
/// предпросмотр/состояние VCB для отрисовки.
/// </summary>
public sealed class ToolController
{
    public ToolController(Scene scene, UndoStack undoStack)
    {
        Context = new ToolContext(scene, undoStack);
    }

    /// <summary>Контекст инструментов (сцена, выборка, VCB, история).</summary>
    public ToolContext Context { get; }

    /// <summary>Активный инструмент или <c>null</c>.</summary>
    public ITool? ActiveTool { get; private set; }

    public bool IsActive => ActiveTool is not null;

    /// <summary>Активный инструмент, его предпросмотр или VCB изменились.</summary>
    public event Action? Changed;

    public void Activate(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (ReferenceEquals(ActiveTool, tool))
        {
            return;
        }

        Deactivate();
        tool.Activate(Context);
        tool.StateChanged += OnToolStateChanged;
        ActiveTool = tool;
        Changed?.Invoke();
    }

    public void Deactivate()
    {
        if (ActiveTool is not { } tool)
        {
            return;
        }

        tool.StateChanged -= OnToolStateChanged;
        tool.Deactivate();
        ActiveTool = null;
        Changed?.Invoke();
    }

    /// <summary>Событие указателя → активному инструменту; <c>true</c> — потреблено.</summary>
    public bool RoutePointer(PointerInput input) => ActiveTool?.OnPointer(input) ?? false;

    /// <summary>Событие клавиши → активному инструменту; <c>true</c> — потреблено.</summary>
    public bool RouteKey(KeyInput input) => ActiveTool?.OnKey(input) ?? false;

    /// <summary>Числовое значение VCB (мм) → активному инструменту; <c>true</c> — применено.</summary>
    public bool CommitValue(double value) => ActiveTool?.TryApplyValue(value) ?? false;

    /// <summary>Отмена текущего действия активного инструмента.</summary>
    public void Cancel() => ActiveTool?.Cancel();

    public ToolPreview Preview => ActiveTool?.Preview ?? ToolPreview.Empty;

    public ToolVcbState Vcb => ActiveTool?.Vcb ?? ToolVcbState.Inactive;

    private void OnToolStateChanged() => Changed?.Invoke();
}
