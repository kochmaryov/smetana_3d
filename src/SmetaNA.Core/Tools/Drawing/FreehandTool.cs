using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Кривая» (Freehand, задача 2.1): штрих перетаскиванием с зажатой
/// кнопкой — точки ложатся на плоскость построения (грань под курсором или
/// «земля» XY) с адаптивным шагом по пиксельному масштабу. Отпускание
/// завершает штрих одной командой истории; конец у начала замыкает контур
/// (на плоском штрихе — создаётся грань). Escape отменяет, Delete убирает
/// последнюю точку. Числовой ввод VCB не применяется.
/// </summary>
public sealed class FreehandTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Кривая»).</summary>
    public const string ToolId = "tool.freehand";

    /// <summary>Имя создаваемого узла сцены (штрих в пустоте).</summary>
    public const string NodeName = "Кривая";

    /// <summary>Минимальный шаг штриха, мм (без вьюпорта).</summary>
    public const double MinStepMm = 0.5;

    /// <summary>Шаг штриха в пикселях экрана (адаптивный фильтр точек).</summary>
    public const double MinStepPx = 2.0;

    private readonly List<Vector3d> _stroke = [];
    private bool _pressed;

    public override string Id => ToolId;

    public override bool IsInteracting => _pressed;

    /// <summary>Точки текущего штриха в мировых координатах.</summary>
    public IReadOnlyList<Vector3d> Stroke => _stroke;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return BeginStroke(input.PositionPx);

            case PointerEventKind.Move when _pressed:
                return AppendSample(input.PositionPx);

            case PointerEventKind.Release when input.Button == PointerButton.Left && _pressed:
                return EndStroke(input.PositionPx);

            case PointerEventKind.Leave when _pressed:
                return true;

            default:
                return false;
        }
    }

    public override bool OnKey(KeyInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Key)
        {
            case ToolKey.Escape:
                Cancel();
                return true;

            case ToolKey.Delete:
                if (_pressed && _stroke.Count > 1)
                {
                    _stroke.RemoveAt(_stroke.Count - 1);
                    UpdatePreview();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>Числовой ввод VCB штрихом не применяется.</summary>
    public override bool TryApplyValue(double value) => false;

    /// <summary>Отмена: незавершённый штрих отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _pressed = false;
        _stroke.Clear();
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool BeginStroke(Vector2d positionPx)
    {
        if (_pressed)
        {
            return true;
        }

        ResolveSurface(positionPx);
        _stroke.Clear();
        _stroke.Add(OnPlane(ResolvePoint(positionPx, null)));
        _pressed = true;
        SetVcb(ToolValueKind.Length, null, false);
        UpdatePreview();
        return true;
    }

    private bool AppendSample(Vector2d positionPx)
    {
        Vector3d sample = OnPlane(ResolvePoint(positionPx, null));
        if ((sample - _stroke[^1]).Length() < MinStep(sample))
        {
            return true;
        }

        _stroke.Add(sample);
        UpdatePreview();
        return true;
    }

    private bool EndStroke(Vector2d positionPx)
    {
        _pressed = false;
        AppendSample(positionPx);

        if (_stroke.Count < 2)
        {
            Cancel();
            return true;
        }

        bool closed = _stroke.Count >= 3 && IsNearInPixels(positionPx, _stroke[0]);
        var points = new List<Vector3d>(_stroke);
        if (closed)
        {
            // конец штриха возле начала — кольцо замыкается на точную первую точку
            if (points.Count >= 4)
            {
                points.RemoveAt(points.Count - 1);
            }
            else
            {
                closed = false;
            }
        }

        bool createFace = closed && DrawingShapes.IsPlanar(points);
        string description = closed ? "Кривая (замкнутая)" : "Кривая";

        CommitShape(description, NodeName, closed
            ? DrawingShapeSpec.Closed(points, createFace)
            : DrawingShapeSpec.Open(points));
        _stroke.Clear();
        ResetSurface();
        SetPreview(ToolPreview.Empty);
        return true;
    }

    private double MinStep(Vector3d point)
        => Viewport?.WorldRadiusAt(point, MinStepPx) is { } step && step > 0 ? step : MinStepMm;

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        AppendLines(lines, _stroke, ToolPreviewStyle.Highlight);
        var points = new List<ToolPreviewPoint>();
        if (_stroke.Count > 0)
        {
            points.Add(new ToolPreviewPoint(_stroke[0], ToolPreviewStyle.Default));
            points.Add(new ToolPreviewPoint(_stroke[^1], ToolPreviewStyle.Highlight));
        }

        SetPreview(new ToolPreview(lines, points));
    }
}
