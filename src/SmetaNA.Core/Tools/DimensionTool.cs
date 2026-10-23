using System.Globalization;
using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools;

/// <summary>Состояние построения размера.</summary>
public enum DimensionToolState
{
    /// <summary>Точек нет.</summary>
    Idle,

    /// <summary>Выбрано начало, выбирается конец.</summary>
    StartPicked,

    /// <summary>Выбраны начало и конец, размещается размерная линия.</summary>
    EndPicked,
}

/// <summary>
/// Инструмент создания размера (задача 3.1): выбор двух точек или ребра через
/// привязки <see cref="InferenceEngine"/>, размещение размерной линии, точный
/// числовой ввод VCB (длина измеряемого отрезка, затем смещение размерной линии).
/// <para>
/// Клик без точечной привязки над ребром задаёт измеряемый отрезок по ребру
/// (концы ребра); в середине построения клик по ребру ставит конец в ближайшую
/// точку ребра. Изменение сцены — одной командой <see cref="AddAnnotationCommand"/>
/// (undo/redo).
/// </para>
/// </summary>
public sealed class DimensionTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Размер»).</summary>
    public const string ToolId = "tool.dimension";

    private DimensionToolState _state = DimensionToolState.Idle;
    private Vector3d _start;
    private Vector3d _end;
    private Vector3d _lineOffset;
    private Vector3d _cursorPoint;
    private bool _hasCursor;

    public override string Id => ToolId;

    public override bool IsInteracting => _state != DimensionToolState.Idle;

    /// <summary>Состояние построения.</summary>
    public DimensionToolState State => _state;

    /// <summary>Вид создаваемого размера (по умолчанию — выровненный).</summary>
    public DimensionKind Kind { get; set; } = DimensionKind.Aligned;

    /// <summary>Начало измеряемого отрезка (после первого клика).</summary>
    public Vector3d Start => _start;

    /// <summary>Конец измеряемого отрезка (после второго клика).</summary>
    public Vector3d End => _end;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                UpdateCursor(input.PositionPx);
                return _state != DimensionToolState.Idle;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры, инструмент не перехватывает
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                return Click(input.PositionPx);

            case PointerEventKind.Leave:
                _hasCursor = false;
                return _state != DimensionToolState.Idle;

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
                if (_state == DimensionToolState.Idle)
                {
                    return false;
                }

                StepBack();
                return true;

            case ToolKey.Enter when _state == DimensionToolState.EndPicked:
                Commit();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB (мм): после первого клика задаёт точную длину
    /// измеряемого отрезка (конец — вдоль текущего направления), при размещении
    /// размерной линии — точное смещение линии (со знаком).
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        switch (_state)
        {
            case DimensionToolState.StartPicked:
                if (value <= DimensionMath.MinLengthMm)
                {
                    return false;
                }

                _end = _start + MeasureDirection() * value;
                _lineOffset = Vector3d.Zero;
                _state = DimensionToolState.EndPicked;
                SetVcb(ToolValueKind.Length, 0, true);
                BuildPreview();
                return true;

            case DimensionToolState.EndPicked:
                Vector3d offsetDirection = _lineOffset.LengthSquared() > 0
                    ? _lineOffset.Normalized()
                    : DefaultPerpendicular(MeasureDirection());
                _lineOffset = offsetDirection * value;
                Commit();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Отмена: построение сбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _state = DimensionToolState.Idle;
        _start = Vector3d.Zero;
        _end = Vector3d.Zero;
        _lineOffset = Vector3d.Zero;
        _hasCursor = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool Click(Vector2d positionPx)
    {
        InferenceScene scene = Context.BuildInferenceScene();
        bool hasAnchor = _state == DimensionToolState.StartPicked;
        if (!DimensionPickSupport.TryPickPoint(Context, scene, positionPx, hasAnchor ? _start : null, out DimensionPickResult pick))
        {
            return false;
        }

        switch (_state)
        {
            case DimensionToolState.Idle:
                if (pick.Edge is { } edge)
                {
                    // Клик по ребру без точечной привязки — размер по ребру целиком.
                    _start = edge.Start;
                    _end = edge.End;
                    _cursorPoint = pick.Point;
                    _hasCursor = true;
                    _state = DimensionToolState.EndPicked;
                    SetVcb(ToolValueKind.Length, MeasureLength(), true);
                    BuildPreview();
                    return true;
                }

                _start = pick.Point;
                _lineOffset = Vector3d.Zero;
                _hasCursor = false;
                _state = DimensionToolState.StartPicked;
                SetVcb(ToolValueKind.Length, null, true);
                SetPreview(ToolPreview.Empty);
                return true;

            case DimensionToolState.StartPicked:
                _end = pick.Point;
                if (MeasureLength() <= DimensionMath.MinLengthMm)
                {
                    return true;
                }

                _cursorPoint = pick.Point;
                _hasCursor = true;
                _lineOffset = Vector3d.Zero;
                _state = DimensionToolState.EndPicked;
                SetVcb(ToolValueKind.Length, _lineOffset.Length(), true);
                BuildPreview();
                return true;

            case DimensionToolState.EndPicked:
                _cursorPoint = pick.Point;
                _hasCursor = true;
                _lineOffset = PerpendicularOffset(_cursorPoint);
                Commit();
                return true;

            default:
                return false;
        }
    }

    private void Commit()
    {
        if (_state != DimensionToolState.EndPicked || MeasureLength() <= DimensionMath.MinLengthMm)
        {
            Cancel();
            return;
        }

        var node = new DimensionNode
        {
            Name = "Dimension",
            Kind = Kind,
            Start = _start,
            End = _end,
            MeasureDirection = Kind == DimensionKind.Linear ? MeasureDirection() : null,
            LineOffset = _lineOffset,
        };

        double value = node.ValueMm;
        string description = string.Format(CultureInfo.InvariantCulture, "Размер {0:0.###} мм", value);
        Context.Execute(new AddAnnotationCommand(Context.Scene.Root, node, description));

        _state = DimensionToolState.Idle;
        _start = Vector3d.Zero;
        _end = Vector3d.Zero;
        _lineOffset = Vector3d.Zero;
        _hasCursor = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void StepBack()
    {
        if (_state == DimensionToolState.EndPicked)
        {
            _state = DimensionToolState.StartPicked;
            _lineOffset = Vector3d.Zero;
            SetVcb(ToolValueKind.Length, null, true);
            SetPreview(ToolPreview.Empty);
            return;
        }

        Cancel();
    }

    private void UpdateCursor(Vector2d positionPx)
    {
        InferenceScene scene = Context.BuildInferenceScene();
        bool hasAnchor = _state == DimensionToolState.StartPicked;
        if (!DimensionPickSupport.TryPickPoint(Context, scene, positionPx, hasAnchor ? _start : null, out DimensionPickResult pick))
        {
            return;
        }

        _cursorPoint = pick.Point;
        _hasCursor = true;

        switch (_state)
        {
            case DimensionToolState.StartPicked:
                SetVcb(ToolValueKind.Length, MeasureLength(), true);
                BuildPreview();
                break;

            case DimensionToolState.EndPicked:
                _lineOffset = PerpendicularOffset(_cursorPoint);
                SetVcb(ToolValueKind.Length, _lineOffset.Length(), true);
                BuildPreview();
                break;
        }
    }

    private double MeasureLength() => DimensionMath.MeasuredLength(Kind, _start, EndForMeasure(), MeasureDirection());

    /// <summary>Конец отрезка для текущего шага: на шаге конца — точка курсора.</summary>
    private Vector3d EndForMeasure()
        => _state == DimensionToolState.StartPicked && _hasCursor ? _cursorPoint : _end;

    private Vector3d MeasureDirection()
    {
        if (_state == DimensionToolState.StartPicked && !_hasCursor)
        {
            return Vector3d.UnitX;
        }

        return DimensionMath.DirectionOf(_start, EndForMeasure(), null);
    }

    /// <summary>Смещение размерной линии: перпендикулярная компонента точки к линии измерения.</summary>
    private Vector3d PerpendicularOffset(Vector3d point)
    {
        Vector3d dir = MeasureDirection();
        Vector3d fromStart = point - _start;
        return fromStart - dir * Vector3d.Dot(fromStart, dir);
    }

    private static Vector3d DefaultPerpendicular(Vector3d direction)
    {
        Vector3d reference = Math.Abs(Vector3d.Dot(direction, Vector3d.UnitZ)) > 0.9
            ? Vector3d.UnitY
            : Vector3d.UnitZ;
        Vector3d perpendicular = Vector3d.Cross(direction, reference);
        return perpendicular.LengthSquared() > 0 ? perpendicular.Normalized() : Vector3d.UnitX;
    }

    private void BuildPreview()
    {
        if (_state == DimensionToolState.Idle)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        if (_state == DimensionToolState.StartPicked)
        {
            Vector3d end = EndForMeasure();
            string text = DimensionText.Format(MeasureLength(), Context.Scene.Unit);
            SetPreview(new ToolPreview(
                [new ToolPreviewLine(_start, end, ToolPreviewStyle.Guide)],
                [new ToolPreviewPoint(_start, ToolPreviewStyle.Highlight)],
                [new ToolPreviewLabel(end, text)]));
            return;
        }

        Vector3d measuredEnd = DimensionMath.MeasuredEnd(Kind, _start, _end, MeasureDirection());
        Vector3d lineStart = _start + _lineOffset;
        Vector3d lineEnd = measuredEnd + _lineOffset;
        string valueText = DimensionText.Format(MeasureLength(), Context.Scene.Unit);
        SetPreview(new ToolPreview(
            [
                new ToolPreviewLine(_start, lineStart, ToolPreviewStyle.Guide),
                new ToolPreviewLine(measuredEnd, lineEnd, ToolPreviewStyle.Guide),
                new ToolPreviewLine(lineStart, lineEnd, ToolPreviewStyle.Default),
            ],
            [
                new ToolPreviewPoint(_start, ToolPreviewStyle.Highlight),
                new ToolPreviewPoint(measuredEnd, ToolPreviewStyle.Highlight),
            ],
            [new ToolPreviewLabel((lineStart + lineEnd) * 0.5, valueText)]));
    }
}
