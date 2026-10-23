using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Окружность» (задача 2.1): первый клик — центр (фиксирует
/// плоскость построения: грань под курсором или «земля» XY), движение/второй
/// клик — радиус; привязки (центр, концы, оси, касательные) учитываются.
/// Готовая окружность — грань из N сегментов (вершины точно на радиусе),
/// одна команда истории.
/// <para>VCB: радиус в мм — значение сразу завершает фигуру.</para>
/// </summary>
public sealed class CircleTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Окружность»).</summary>
    public const string ToolId = "tool.circle";

    /// <summary>Имя создаваемого узла сцены (окружность в пустоте).</summary>
    public const string NodeName = "Окружность";

    private readonly int _segments;
    private bool _started;
    private Vector3d _center;
    private double _radius;

    /// <param name="segments">Число сегментов окружности (по умолчанию 24).</param>
    public CircleTool(int segments = DrawingShapes.DefaultCircleSegments)
    {
        if (segments < DrawingShapes.MinPolygonSides)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), "Окружности нужно минимум 3 сегмента.");
        }

        _segments = segments;
    }

    public override string Id => ToolId;

    public override bool IsInteracting => _started;

    /// <summary>Центр окружности (мировые координаты).</summary>
    public Vector3d Center => _center;

    /// <summary>Текущий радиус, мм.</summary>
    public double Radius => _radius;

    /// <summary>Число сегментов окружности.</summary>
    public int Segments => _segments;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (!_started)
                {
                    return false;
                }

                UpdateRadius(input.PositionPx, input.Modifiers);
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
                if (_started && _radius > MinSegmentLengthMm)
                {
                    Commit();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>Числовой ввод VCB: радиус в мм, значение сразу завершает окружность.</summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || value <= 0 || !_started)
        {
            return false;
        }

        _radius = value;
        Commit();
        return true;
    }

    /// <summary>Отмена: недостроенная окружность отбрасывается.</summary>
    public override void Cancel()
    {
        _started = false;
        _radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        if (!_started)
        {
            ResolveSurface(positionPx);
            _center = OnPlane(ResolvePoint(positionPx, null, modifiers));
            _radius = 0;
            _started = true;
            SetVcb(ToolValueKind.Length, null, true);
            UpdatePreview();
            return true;
        }

        UpdateRadius(positionPx, modifiers);
        if (_radius > MinSegmentLengthMm)
        {
            Commit();
        }

        return true;
    }

    private bool OnRightPress()
    {
        if (!_started)
        {
            return false;
        }

        if (_radius > MinSegmentLengthMm)
        {
            Commit();
        }
        else
        {
            Cancel();
        }

        return true;
    }

    private void UpdateRadius(Vector2d positionPx, ToolModifiers modifiers)
    {
        IReadOnlyList<SnapCircle> circles = _radius > 0
            ? [new SnapCircle(_center, Plane.Normal, _radius)]
            : [];
        Vector3d point = OnPlane(ResolvePoint(positionPx, _center, modifiers, circles, []));
        (double U, double V) center = Plane.Coordinates(_center);
        (double U, double V) cursor = Plane.Coordinates(point);
        double radius = Math.Sqrt(
            ((cursor.U - center.U) * (cursor.U - center.U))
            + ((cursor.V - center.V) * (cursor.V - center.V)));
        if (radius <= MinSegmentLengthMm)
        {
            return;
        }

        _radius = radius;
        SetVcb(ToolValueKind.Length, radius, true);
        UpdatePreview();
    }

    private void Commit()
    {
        IReadOnlyList<Vector3d> ring = DrawingShapes.CircleRing(Plane, _center, _radius, _segments);
        string description = string.Format(
            System.Globalization.CultureInfo.InvariantCulture, "Окружность R{0:0.###} мм", _radius);

        CommitShape(description, NodeName, DrawingShapeSpec.Closed(ring));
        _started = false;
        _radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();
        if (_radius > MinSegmentLengthMm)
        {
            IReadOnlyList<Vector3d> ring = DrawingShapes.CircleRing(Plane, _center, _radius, _segments);
            AppendLines(lines, ring, ToolPreviewStyle.Highlight, close: true);
            lines.Add(new ToolPreviewLine(_center, ring[0], ToolPreviewStyle.Guide));
            labels.Add(LabelAt(ring[0], string.Format(
                System.Globalization.CultureInfo.InvariantCulture, "R{0:0.###} mm", _radius)));
        }

        points.Add(new ToolPreviewPoint(_center, ToolPreviewStyle.Default));
        SetPreview(new ToolPreview(lines, points, labels));
    }
}
