using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Дуга (2 точки)» (задача 2.1): клики ставят начало и конец хорды,
/// движение задаёт высоту дуги (выпуклость — в сторону курсора); привязки
/// (концы, середины, оси, касательные к строящейся дуге) учитываются. Дуга —
/// ломаная сегментов на окружности (концы точные), одна команда истории.
/// <para>
/// VCB: центральный угол дуги в градусах, (0; 180]; радиус выводится как
/// R = хорда / (2·sin(угол/2)). Значение сразу завершает дугу.
/// </para>
/// </summary>
public sealed class Arc2PointTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Дуга»).</summary>
    public const string ToolId = "tool.arc";

    /// <summary>Имя создаваемого узла сцены (дуга в пустоте).</summary>
    public const string NodeName = "Дуга";

    private Vector3d _start;
    private Vector3d _end;
    private Vector3d _toward;
    private bool _hasStart;
    private bool _hasEnd;

    public override string Id => ToolId;

    public override bool IsInteracting => _hasStart;

    /// <summary>Начало хорды (мировые координаты).</summary>
    public Vector3d Start => _start;

    /// <summary>Конец хорды (мировые координаты).</summary>
    public Vector3d End => _end;

    /// <summary>Текущая высота дуги (саггита), мм.</summary>
    public double BulgeHeight { get; private set; }

    /// <summary>Текущий радиус дуги, мм.</summary>
    public double Radius { get; private set; }

    /// <summary>Текущий центральный угол дуги, градусы.</summary>
    public double SweepDeg { get; private set; }

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (!_hasEnd)
                {
                    return false;
                }

                UpdateBulge(input.PositionPx, input.Modifiers);
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
                if (_hasEnd && Radius > MinSegmentLengthMm)
                {
                    Commit();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB: центральный угол в градусах, (0; 180]; значение сразу
    /// завершает дугу (выпуклость — в последнюю сторону курсора).
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || !_hasEnd)
        {
            return false;
        }

        double angle = Math.Clamp(value, MinSweepDeg, 180);
        double chord = (_end - _start).Length();
        SweepDeg = angle;
        Radius = chord / (2.0 * Math.Sin(angle * Math.PI / 360.0));
        BulgeHeight = Radius * (1.0 - Math.Cos(angle * Math.PI / 360.0));
        IReadOnlyList<Vector3d> arc = DrawingShapes.ArcChordAngle(_start, _end, angle, _toward, Plane.Normal);
        CommitArc(arc);
        return true;
    }

    /// <summary>Отмена: недостроенная дуга отбрасывается.</summary>
    public override void Cancel()
    {
        _hasStart = false;
        _hasEnd = false;
        BulgeHeight = 0;
        Radius = 0;
        SweepDeg = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    /// <summary>Минимальный центральный угол VCB, градусы.</summary>
    public const double MinSweepDeg = 1.0;

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        if (!_hasStart)
        {
            ResolveSurface(positionPx);
            _start = ResolvePoint(positionPx, null, modifiers);
            _toward = _start + Plane.U;
            _hasStart = true;
            SetVcb(ToolValueKind.None, null, true);
            UpdatePreview();
            return true;
        }

        if (!_hasEnd)
        {
            Vector3d end = ResolvePoint(positionPx, _start, modifiers);
            if ((end - _start).Length() <= MinSegmentLengthMm)
            {
                return true;
            }

            _end = end;
            _hasEnd = true;
            _toward = OnPlane(ResolvePoint(positionPx, _start, modifiers, [], ChordSegments()));
            SetVcb(ToolValueKind.Angle, null, true);
            UpdateBulge(positionPx, modifiers);
            return true;
        }

        UpdateBulge(positionPx, modifiers);
        if (Radius > MinSegmentLengthMm)
        {
            Commit();
        }

        return true;
    }

    private bool OnRightPress()
    {
        if (!_hasStart)
        {
            return false;
        }

        if (_hasEnd && Radius > MinSegmentLengthMm)
        {
            Commit();
        }
        else
        {
            Cancel();
        }

        return true;
    }

    private void UpdateBulge(Vector2d positionPx, ToolModifiers modifiers)
    {
        _toward = OnPlane(ResolvePoint(positionPx, _start, modifiers, [], ChordSegments()));
        Vector3d chord = _end - _start;
        double chordLength = chord.Length();
        if (chordLength <= 0)
        {
            return;
        }

        Vector3d midpoint = (_start + _end) * 0.5;
        double height = Math.Abs(Vector3d.Dot(_toward - midpoint, Perpendicular()));
        if (height <= MinSegmentLengthMm)
        {
            return;
        }

        BulgeHeight = height;
        Radius = ((chordLength * chordLength * 0.25) + (height * height)) / (2.0 * height);
        SweepDeg = Math.Acos(Math.Clamp(1.0 - (height / Radius), -1.0, 1.0)) * 360.0 / Math.PI;
        SetVcb(ToolValueKind.Angle, SweepDeg, true);
        UpdatePreview();
    }

    private void Commit()
    {
        IReadOnlyList<Vector3d> arc = DrawingShapes.ArcChordRadius(_start, _end, Radius, _toward, Plane.Normal);
        CommitArc(arc);
    }

    private void CommitArc(IReadOnlyList<Vector3d> arc)
    {
        if (arc.Count < 2)
        {
            return;
        }

        string description = string.Format(
            System.Globalization.CultureInfo.InvariantCulture, "Дуга R{0:0.###} мм", Radius);
        CommitShape(description, NodeName, DrawingShapeSpec.Open(arc));
        _hasStart = false;
        _hasEnd = false;
        BulgeHeight = 0;
        Radius = 0;
        SweepDeg = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private Vector3d Perpendicular()
    {
        Vector3d normal = Vector3d.Cross(_end - _start, _toward - _start);
        if (normal.LengthSquared() <= 0)
        {
            normal = Plane.Normal;
        }

        return Vector3d.Cross(normal.Normalized(), _end - _start).Normalized();
    }

    private IReadOnlyList<SnapSegment> ChordSegments() => _hasStart && _hasEnd
        ? [new SnapSegment(_start, _end)]
        : [];

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        points.Add(new ToolPreviewPoint(_start, ToolPreviewStyle.Default));
        if (!_hasEnd)
        {
            SetPreview(new ToolPreview(lines, points, labels));
            return;
        }

        points.Add(new ToolPreviewPoint(_end, ToolPreviewStyle.Default));
        lines.Add(new ToolPreviewLine(_start, _end, ToolPreviewStyle.Guide));
        if (Radius > MinSegmentLengthMm)
        {
            try
            {
                IReadOnlyList<Vector3d> arc = DrawingShapes.ArcChordRadius(_start, _end, Radius, _toward, Plane.Normal);
                AppendLines(lines, arc, ToolPreviewStyle.Highlight);
                labels.Add(LabelAt(arc[arc.Count / 2], string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "R{0:0.###} mm / {1:0.#}°",
                    Radius,
                    SweepDeg)));
            }
            catch (ArgumentException)
            {
                // радиус меньше половины хорды — предпросмотр дуги не строится
            }
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
