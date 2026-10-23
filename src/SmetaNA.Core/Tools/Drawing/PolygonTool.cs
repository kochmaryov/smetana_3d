using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Многоугольник» (задача 2.1): правильный n-угольник по центру и
/// радиусу (до вершин, описанная окружность) в плоскости построения (грань под
/// курсором или «земля» XY). Привязки (центр, концы, оси, касательные)
/// учитываются. Готовый многоугольник — грань, одна команда истории.
/// <para>
/// VCB: до выбора центра — целое число сторон (3…64, по умолчанию 6); после
/// центра — радиус в мм, значение сразу завершает фигуру.
/// </para>
/// </summary>
public sealed class PolygonTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Многоугольник»).</summary>
    public const string ToolId = "tool.polygon";

    /// <summary>Имя создаваемого узла сцены (многоугольник в пустоте).</summary>
    public const string NodeName = "Многоугольник";

    /// <summary>Число сторон по умолчанию.</summary>
    public const int DefaultSides = 6;

    private bool _started;
    private Vector3d _center;
    private double _radius;
    private int _sides = DefaultSides;

    public override string Id => ToolId;

    public override bool IsInteracting => _started;

    /// <summary>Число сторон многоугольника.</summary>
    public int Sides => _sides;

    /// <summary>Центр многоугольника (мировые координаты).</summary>
    public Vector3d Center => _center;

    /// <summary>Текущий радиус (до вершин), мм.</summary>
    public double Radius => _radius;

    protected override void OnActivated() => SetVcb(ToolValueKind.Count, _sides, true);

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

    /// <summary>
    /// Числовой ввод VCB: до центра — число сторон (целое, 3…64); после центра —
    /// радиус в мм, значение сразу завершает многоугольник.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        if (!_started)
        {
            _sides = Math.Clamp((int)Math.Round(value), DrawingShapes.MinPolygonSides, DrawingShapes.MaxPolygonSides);
            SetVcb(ToolValueKind.Count, _sides, true);
            UpdatePreview();
            return true;
        }

        if (value <= 0)
        {
            return false;
        }

        _radius = value;
        Commit();
        return true;
    }

    /// <summary>Отмена: недостроенный многоугольник отбрасывается.</summary>
    public override void Cancel()
    {
        _started = false;
        _radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.Count, _sides, true);
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
        IReadOnlyList<Vector3d> ring = DrawingShapes.PolygonRing(Plane, _center, _radius, _sides);
        string description = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Многоугольник ({0}) R{1:0.###} мм",
            _sides,
            _radius);

        CommitShape(description, NodeName, DrawingShapeSpec.Closed(ring));
        _started = false;
        _radius = 0;
        ResetSurface();
        SetVcb(ToolValueKind.Count, _sides, true);
        SetPreview(ToolPreview.Empty);
    }

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();
        if (_radius > MinSegmentLengthMm)
        {
            IReadOnlyList<Vector3d> ring = DrawingShapes.PolygonRing(Plane, _center, _radius, _sides);
            AppendLines(lines, ring, ToolPreviewStyle.Highlight, close: true);
            lines.Add(new ToolPreviewLine(_center, ring[0], ToolPreviewStyle.Guide));
            labels.Add(LabelAt(ring[0], string.Format(
                System.Globalization.CultureInfo.InvariantCulture, "R{0:0.###} mm", _radius)));
        }

        if (_started)
        {
            points.Add(new ToolPreviewPoint(_center, ToolPreviewStyle.Default));
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}