using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
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

    private PointerInput? _lastPointer;

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
    public bool RoutePointer(PointerInput input)
    {
        if (input.Kind == PointerEventKind.Press)
        {
            // клик применяет привязку: Shift не должен «прилипать» к прошлой точке
            ActiveTool?.ResetSnap();
        }

        if (input.Kind == PointerEventKind.Move)
        {
            // Указатель сдвинулся — перебор кандидатов Tab начинается заново.
            Context.ResetSnapCandidateOffset();
            _lastPointer = input;
        }

        return ActiveTool?.OnPointer(input) ?? false;
    }

    /// <summary>
    /// Событие клавиши: стрелки фиксируют оси, Alt переключает режим привязок,
    /// Tab перебирает кандидатов, Esc снимает фиксацию; остальное — активному инструменту.
    /// </summary>
    public bool RouteKey(KeyInput input)
    {
        if (ActiveTool is null)
        {
            return false;
        }

        switch (input.Key)
        {
            case ToolKey.Right:
                return ToggleSnapLock(SnapModifiers.LockAxisX);
            case ToolKey.Left:
                return ToggleSnapLock(SnapModifiers.LockAxisY);
            case ToolKey.Up:
                return ToggleSnapLock(SnapModifiers.LockAxisZ);
            case ToolKey.Down:
                return ToggleSnapLock(SnapModifiers.LockParallel | SnapModifiers.LockPerpendicular);
            case ToolKey.Alt:
                Context.CycleInferenceMode();
                Changed?.Invoke();
                return true;
            case ToolKey.Tab:
                return CycleSnapCandidate();
            case ToolKey.Escape:
                Context.ResetSnapLocks();
                break;
        }

        return ActiveTool.OnKey(input);
    }

    /// <summary>
    /// Следующий кандидат привязки (Tab): пересчитать привязку под курсором, не
    /// сбрасывая выбранный индекс.
    /// </summary>
    private bool CycleSnapCandidate()
    {
        if (_lastPointer is not { } pointer)
        {
            return false;
        }

        Context.CycleSnapCandidate();
        ActiveTool?.OnPointer(pointer with { Kind = PointerEventKind.Move });
        Changed?.Invoke();
        return true;
    }

    private bool ToggleSnapLock(SnapModifiers lockModifiers)
    {
        Context.ToggleSnapLock(lockModifiers);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Числовое значение VCB (мм) → активному инструменту; <c>true</c> — применено.</summary>
    public bool CommitValue(double value) => ActiveTool?.TryApplyValue(value) ?? false;

    /// <summary>
    /// Координаты из VCB → активному инструменту; <c>true</c> — применено.
    /// В локальном режиме (R-16, S14) строка пересчитывается в мировые оси.
    /// </summary>
    public bool CommitPoint(Vector3d point, bool relative)
    {
        Vector3d world = relative ? Context.ToWorldVector(point) : Context.ToWorldPoint(point);
        return ActiveTool?.TryApplyPoint(world, relative) ?? false;
    }

    /// <summary>Отмена текущего действия активного инструмента.</summary>
    public void Cancel() => ActiveTool?.Cancel();

    public ToolPreview Preview => ActiveTool?.Preview ?? ToolPreview.Empty;

    /// <summary>Текущий результат привязок активного инструмента (для подсказок вьюпорта).</summary>
    public SnapResult Snap => ActiveTool?.CurrentSnap ?? SnapResult.Empty;

    public ToolVcbState Vcb => ActiveTool?.Vcb ?? ToolVcbState.Inactive;

    private void OnToolStateChanged() => Changed?.Invoke();
}
