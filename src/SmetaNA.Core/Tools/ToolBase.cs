namespace SmetaNA.Core.Tools;

/// <summary>
/// База инструментов: хранит контекст, предпросмотр и состояние VCB,
/// роутит события <see cref="StateChanged"/>. Наследники реализуют
/// обработку ввода и отмену.
/// </summary>
public abstract class ToolBase : ITool
{
    private ToolContext? _context;
    private ToolPreview _preview = ToolPreview.Empty;
    private ToolVcbState _vcb = ToolVcbState.Inactive;

    public abstract string Id { get; }

    public virtual bool IsInteracting => false;

    public ToolVcbState Vcb => _vcb;

    public ToolPreview Preview => _preview;

    public event Action? StateChanged;

    /// <summary>Контекст активного инструмента.</summary>
    protected ToolContext Context =>
        _context ?? throw new InvalidOperationException("Инструмент не активен: контекст недоступен.");

    protected bool IsActive => _context is not null;

    public void Activate(ToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
        {
            throw new InvalidOperationException("Инструмент уже активен.");
        }

        _context = context;
        OnActivated();
    }

    public void Deactivate()
    {
        if (_context is not null)
        {
            Cancel();
            OnDeactivated();
            _context.ReportVcb(ToolValueKind.None, null, false);
            _context = null;
            _vcb = ToolVcbState.Inactive;
            _preview = ToolPreview.Empty;
            RaiseStateChanged();
        }
    }

    public abstract void Cancel();

    public abstract bool OnPointer(PointerInput input);

    public virtual bool OnKey(KeyInput input) => false;

    public abstract bool TryApplyValue(double value);

    /// <summary>Вызывается при активации инструмента.</summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>Вызывается при деактивации (после <see cref="Cancel"/>).</summary>
    protected virtual void OnDeactivated()
    {
    }

    protected void SetPreview(ToolPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (ReferenceEquals(_preview, preview))
        {
            return;
        }

        _preview = preview;
        RaiseStateChanged();
    }

    protected void SetVcb(ToolValueKind kind, double? value, bool isActive)
    {
        ToolVcbState state = new(isActive, kind, value);
        if (_vcb == state)
        {
            return;
        }

        _vcb = state;
        _context?.ReportVcb(kind, value, isActive);
        RaiseStateChanged();
    }

    protected void RaiseStateChanged() => StateChanged?.Invoke();
}
