using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Прямоугольник» (задача 2.1): первый клик — угол, второй —
/// противоположный; привязки учитываются (курсор проецируется на плоскость
/// построения — грань под курсором или «земля» XY). Готовый прямоугольник —
/// грань из четырёх рёбер, одна команда истории.
/// <para>
/// VCB (мм): первое значение — ширина (вдоль U плоскости), второе — высота
/// (вдоль V) и сразу завершает фигуру; одно значение фиксирует ширину, высота
/// берётся от курсора и подтверждается кликом/Enter.
/// </para>
/// </summary>
public sealed class RectangleTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Прямоугольник»).</summary>
    public const string ToolId = "tool.rectangle";

    /// <summary>Имя создаваемого узла сцены (прямоугольник в пустоте).</summary>
    public const string NodeName = "Прямоугольник";

    private bool _started;
    private Vector3d _corner;
    private Vector3d? _cursor;
    private double? _sizeU;
    private double? _sizeV;
    private bool _vcbOnV;

    /// <summary>Допуск shape inference: отклонение пропорции, при котором она доводится до точной.</summary>
    public const double ShapeTolerance = 0.02;

    /// <summary>Золотое сечение для подсказки «Золотое сечение» (R-35).</summary>
    public const double GoldenRatio = 1.6180339887498949;

    /// <summary>Подсказка формы по текущим размерам (квадрат/золотое сечение).</summary>
    public ShapeHintKind ShapeHint { get; private set; }

    public override string Id => ToolId;

    public override bool IsInteracting => _started;

    /// <summary>Первый угол прямоугольника (мировые координаты).</summary>
    public Vector3d Corner => _corner;

    /// <summary>Противоположный угол по текущему состоянию (мировые координаты).</summary>
    public Vector3d OppositeCorner => _started
        ? Plane.PointAt(CoordinatesOf(_corner).U + SignedSizeU(), CoordinatesOf(_corner).V + SignedSizeV())
        : _corner;

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
                    PreviewSnapAt(input.PositionPx, input.Modifiers);
                    return false;
                }

                UpdateCursor(input.PositionPx, input.Modifiers);
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
                if (_started)
                {
                    Commit();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB (мм): 1-е значение — ширина, 2-е — высота и завершение.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || Math.Abs(value) <= 0 || !_started)
        {
            return false;
        }

        double magnitude = Math.Abs(value);
        if (!_vcbOnV)
        {
            _sizeU = Signed(magnitude, USign());
            _vcbOnV = true;
            SetVcb(ToolValueKind.Length, magnitude, true);
            UpdatePreview();
            return true;
        }

        _sizeV = Signed(magnitude, VSign());
        _vcbOnV = false;
        Commit();
        return true;
    }

    /// <summary>Отмена: недостроенный прямоугольник отбрасывается.</summary>
    public override void Cancel()
    {
        _started = false;
        _cursor = null;
        _sizeU = null;
        _sizeV = null;
        _vcbOnV = false;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        if (!_started)
        {
            ResolveSurface(positionPx);
            _corner = OnPlane(ResolvePoint(positionPx, null, modifiers));
            _cursor = null;
            _sizeU = null;
            _sizeV = null;
            _vcbOnV = false;
            _started = true;
            SetVcb(ToolValueKind.Length, null, true);
            UpdatePreview();
            return true;
        }

        UpdateCursor(positionPx, modifiers);
        Commit();
        return true;
    }

    protected override bool TryApplyPointCore(Vector3d point, bool relative)
    {
        if (!_started)
        {
            if (relative)
            {
                return false;
            }

            _corner = OnPlane(point);
            _cursor = null;
            _sizeU = null;
            _sizeV = null;
            _vcbOnV = false;
            _started = true;
            SetVcb(ToolValueKind.Length, null, true);
            UpdatePreview();
            return true;
        }

        _cursor = OnPlane(relative ? _corner + point : point);
        Commit();
        return true;
    }

    private bool OnRightPress()
    {
        if (!_started)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void UpdateCursor(Vector2d positionPx, ToolModifiers modifiers)
    {
        _cursor = OnPlane(ResolvePoint(positionPx, _corner, modifiers));
        SetVcb(ToolValueKind.Length, Math.Abs(_vcbOnV ? SignedSizeV() : SignedSizeU()), true);
        UpdatePreview();
    }

    private void Commit()
    {
        (double U, double V) anchor = CoordinatesOf(_corner);
        double u2 = anchor.U + SignedSizeU();
        double v2 = anchor.V + SignedSizeV();
        if (Math.Abs(u2 - anchor.U) <= MinSegmentLengthMm || Math.Abs(v2 - anchor.V) <= MinSegmentLengthMm)
        {
            return;
        }

        IReadOnlyList<Vector3d> ring = DrawingShapes.RectRing(Plane, anchor.U, anchor.V, u2, v2);
        double width = Math.Abs(u2 - anchor.U);
        double height = Math.Abs(v2 - anchor.V);
        string description = string.Format(
            CultureInfo.InvariantCulture, "Прямоугольник {0:0.###}×{1:0.###} мм", width, height);

        CommitShape(description, NodeName, DrawingShapeSpec.Closed(ring));
        _started = false;
        _cursor = null;
        _sizeU = null;
        _sizeV = null;
        _vcbOnV = false;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private (double U, double V) CoordinatesOf(Vector3d point) => Plane.Coordinates(point);

    private double SignedSizeU() => _sizeU ?? Shapes().U;

    private double SignedSizeV() => _sizeV ?? Shapes().V;

    /// <summary>
    /// Размеры прямоугольника по курсору с shape inference (R-35): почти квадрат
    /// доводится до точного квадрата, почти золотое сечение — до точного.
    /// Числовой ввод VCB inference не проходит (там размер задан явно).
    /// </summary>
    private (double U, double V) Shapes()
    {
        (double U, double V) delta = CursorDelta();
        if (Math.Abs(delta.U) <= MinSegmentLengthMm || Math.Abs(delta.V) <= MinSegmentLengthMm)
        {
            ShapeHint = ShapeHintKind.None;
            return delta;
        }

        double magnitudeU = Math.Abs(delta.U);
        double magnitudeV = Math.Abs(delta.V);
        double largest = Math.Max(magnitudeU, magnitudeV);

        if (Math.Abs(magnitudeU - magnitudeV) <= largest * ShapeTolerance)
        {
            ShapeHint = ShapeHintKind.Square;
            magnitudeV = magnitudeU;
        }
        else if (Math.Abs((magnitudeU / magnitudeV) - GoldenRatio) <= GoldenRatio * ShapeTolerance)
        {
            ShapeHint = ShapeHintKind.GoldenRatio;
            magnitudeV = magnitudeU / GoldenRatio;
        }
        else if (Math.Abs((magnitudeV / magnitudeU) - GoldenRatio) <= GoldenRatio * ShapeTolerance)
        {
            ShapeHint = ShapeHintKind.GoldenRatio;
            magnitudeU = magnitudeV / GoldenRatio;
        }
        else
        {
            ShapeHint = ShapeHintKind.None;
            return delta;
        }

        return (Signed(magnitudeU, USign()), Signed(magnitudeV, VSign()));
    }

    private (double U, double V) CursorDelta()
    {
        if (_cursor is not { } cursor)
        {
            return (0, 0);
        }

        (double U, double V) anchor = CoordinatesOf(_corner);
        (double U, double V) current = CoordinatesOf(cursor);
        return (current.U - anchor.U, current.V - anchor.V);
    }

    private double USign() => CursorDelta().U < 0 ? -1 : 1;

    private double VSign() => CursorDelta().V < 0 ? -1 : 1;

    private static double Signed(double magnitude, double sign) => magnitude * sign;

    private void UpdatePreview()
    {
        (double U, double V) anchor = CoordinatesOf(_corner);
        double u2 = anchor.U + SignedSizeU();
        double v2 = anchor.V + SignedSizeV();
        var lines = new List<ToolPreviewLine>();
        var labels = new List<ToolPreviewLabel>();

        if (Math.Abs(u2 - anchor.U) > MinSegmentLengthMm && Math.Abs(v2 - anchor.V) > MinSegmentLengthMm)
        {
            IReadOnlyList<Vector3d> ring = DrawingShapes.RectRing(Plane, anchor.U, anchor.V, u2, v2);
            AppendLines(lines, ring, ToolPreviewStyle.Highlight, close: true);
            labels.Add(LabelAt(ring[2], string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###}×{1:0.###} mm",
                Math.Abs(u2 - anchor.U),
                Math.Abs(v2 - anchor.V))));

            // Shape inference (R-35): пропорция доведена до квадрата/золотого сечения.
            string? shapeKey = ShapeHint switch
            {
                ShapeHintKind.Square => "Shape.Square",
                ShapeHintKind.GoldenRatio => "Shape.GoldenRatio",
                _ => null,
            };
            if (shapeKey is not null)
            {
                labels.Add(new ToolPreviewLabel(ring[0], string.Empty, shapeKey));
            }
        }

        SetPreview(new ToolPreview(lines, [new ToolPreviewPoint(_corner, ToolPreviewStyle.Default)], labels));
    }
}
