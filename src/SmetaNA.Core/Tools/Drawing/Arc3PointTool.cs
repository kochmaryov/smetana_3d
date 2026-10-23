using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Дуга (3 точки)» (задача 2.1): клики ставят начало, точку на дуге
/// и конец; окружность строится через три точки, тесселяция проходит через
/// среднюю точку. Привязки (концы, середины, оси, касательные к строящейся
/// дуге) учитываются. Дуга — ломаная сегментов (концы точные), одна команда
/// истории.
/// <para>
/// VCB: радиус итоговой дуги в мм; дуга строится по началу, концу (текущая
/// точка курсора) и радиусу, выпуклость — в сторону средней точки. Значение
/// сразу завершает дугу.
/// </para>
/// </summary>
public sealed class Arc3PointTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Дуга (3 точки)»).</summary>
    public const string ToolId = "tool.arc.3point";

    /// <summary>Имя создаваемого узла сцены (дуга в пустоте).</summary>
    public const string NodeName = "Дуга";

    private Vector3d _start;
    private Vector3d _through;
    private Vector3d _end;
    private bool _hasStart;
    private bool _hasThrough;
    private bool _hasEnd;

    public override string Id => ToolId;

    public override bool IsInteracting => _hasStart;

    /// <summary>Начало дуги (мировые координаты).</summary>
    public Vector3d Start => _start;

    /// <summary>Точка на дуге (мировые координаты).</summary>
    public Vector3d Through => _through;

    /// <summary>Конец дуги (мировые координаты).</summary>
    public Vector3d End => _end;

    /// <summary>Радиус текущей дуги (через три точки), мм; 0 — точки коллинеарны.</summary>
    public double Radius { get; private set; }

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (!_hasThrough)
                {
                    return false;
                }

                UpdateEnd(input.PositionPx, input.Modifiers);
                return true;

            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return OnLeftPress(input.PositionPx, input.Modifiers);

            case PointerEventKind.Press when input.Button == PointerButton.Right:
                return OnRightPress();

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

            case ToolKey.Enter:
                if (_hasThrough && Radius > MinSegmentLengthMm)
                {
                    Commit(DrawingShapes.ArcThrough3Points(_start, _through, _end));
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB: радиус дуги в мм (конец — текущая точка курсора,
    /// выпуклость — в сторону средней точки); значение сразу завершает дугу.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || value <= 0 || !_hasThrough || !_hasEnd)
        {
            return false;
        }

        if ((_end - _start).Length() <= MinSegmentLengthMm)
        {
            return false;
        }

        Radius = value;
        Commit(DrawingShapes.ArcChordRadius(_start, _end, value, _through, Plane.Normal));
        return true;
    }

    /// <summary>Отмена: недостроенная дуга отбрасывается.</summary>
    public override void Cancel()
    {
        _hasStart = false;
        _hasThrough = false;
        _hasEnd = false;
        Radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        if (!_hasStart)
        {
            ResolveSurface(positionPx);
            _start = ResolvePoint(positionPx, null, modifiers);
            _hasStart = true;
            SetVcb(ToolValueKind.None, null, true);
            UpdatePreview();
            return true;
        }

        if (!_hasThrough)
        {
            Vector3d through = ResolvePoint(positionPx, _start, modifiers);
            if ((through - _start).Length() <= MinSegmentLengthMm)
            {
                return true;
            }

            _through = through;
            _hasThrough = true;
            SetVcb(ToolValueKind.Length, null, true);
            UpdateEnd(positionPx, modifiers);
            return true;
        }

        UpdateEnd(positionPx, modifiers);
        if (Radius > MinSegmentLengthMm)
        {
            Commit(DrawingShapes.ArcThrough3Points(_start, _through, _end));
        }

        return true;
    }

    private bool OnRightPress()
    {
        if (!_hasStart)
        {
            return false;
        }

        if (_hasThrough && Radius > MinSegmentLengthMm)
        {
            Commit(DrawingShapes.ArcThrough3Points(_start, _through, _end));
        }
        else
        {
            Cancel();
        }

        return true;
    }

    private void UpdateEnd(Vector2d positionPx, ToolModifiers modifiers)
    {
        IReadOnlyList<SnapCircle> circles = TryCircumcircle(out Vector3d center, out Vector3d normal, out double radius)
            ? [new SnapCircle(center, normal, radius)]
            : [];
        IReadOnlyList<SnapSegment> segments =
        [
            new(_start, _through),
        ];
        _end = ResolvePoint(positionPx, _through, modifiers, circles, segments);
        _hasEnd = true;
        Radius = DrawingShapes.TryCircumcircle(_start, _through, _end, out _, out _, out double circumRadius)
            ? circumRadius
            : 0;
        SetVcb(ToolValueKind.Length, Radius > 0 ? Radius : null, true);
        UpdatePreview();
    }

    private bool TryCircumcircle(out Vector3d center, out Vector3d normal, out double radius)
        => DrawingShapes.TryCircumcircle(_start, _through, _end, out center, out normal, out radius);

    private void Commit(IReadOnlyList<Vector3d>? arc)
    {
        if (arc is not { Count: >= 2 })
        {
            return;
        }

        string description = string.Format(
            System.Globalization.CultureInfo.InvariantCulture, "Дуга (3 точки) R{0:0.###} мм", Radius);
        CommitShape(description, NodeName, DrawingShapeSpec.Open(arc));
        _hasStart = false;
        _hasThrough = false;
        _hasEnd = false;
        Radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>
        {
            new(_start, ToolPreviewStyle.Default),
        };
        var labels = new List<ToolPreviewLabel>();
        if (!_hasThrough)
        {
            SetPreview(new ToolPreview(lines, points, labels));
            return;
        }

        points.Add(new ToolPreviewPoint(_through, ToolPreviewStyle.Default));
        points.Add(new ToolPreviewPoint(_end, ToolPreviewStyle.Default));
        lines.Add(new ToolPreviewLine(_start, _through, ToolPreviewStyle.Guide));
        lines.Add(new ToolPreviewLine(_through, _end, ToolPreviewStyle.Guide));
        if (DrawingShapes.ArcThrough3Points(_start, _through, _end) is { } arc)
        {
            AppendLines(lines, arc, ToolPreviewStyle.Highlight);
            if (Radius > 0)
            {
                labels.Add(LabelAt(arc[arc.Count / 2], string.Format(
                    System.Globalization.CultureInfo.InvariantCulture, "R{0:0.###} mm", Radius)));
            }
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
