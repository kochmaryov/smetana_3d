using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools;

/// <summary>Состояние инструмента масштабирования по размеру.</summary>
public enum ScaleToDimensionToolState
{
    /// <summary>Точек нет.</summary>
    Idle,

    /// <summary>Выбрано начало измеряемого отрезка.</summary>
    StartPicked,

    /// <summary>Измеряемый отрезок выбран, ожидается целевой размер.</summary>
    AwaitingValue,
}

/// <summary>
/// Инструмент «Масштабирование модели по заданному размеру» (задача 3.2):
/// пользователь отмечает две точки (или ребро) и вводит целевой размер числом —
/// геометрия пересчитывается равномерно с сохранением пропорций
/// (<see cref="ScaleModelCommand"/>, undo/redo), первая точка остаётся неподвижной.
/// <para>
/// Точки выбираются через привязки <see cref="InferenceEngine"/> (как у
/// <see cref="DimensionTool"/>): клик без точечной привязки над ребром берёт
/// концы ребра, клик по ребру в середине построения ставит точку на ребро.
/// </para>
/// </summary>
public sealed class ScaleToDimensionTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Масштаб по размеру»).</summary>
    public const string ToolId = "tool.scale_to_dimension";

    private ScaleToDimensionToolState _state = ScaleToDimensionToolState.Idle;
    private Vector3d _start;
    private Vector3d _end;
    private Vector3d _cursorPoint;
    private bool _hasCursor;

    public override string Id => ToolId;

    public override bool IsInteracting => _state != ScaleToDimensionToolState.Idle;

    /// <summary>Состояние построения.</summary>
    public ScaleToDimensionToolState State => _state;

    /// <summary>Начало измеряемого отрезка (неподвижная точка масштаба).</summary>
    public Vector3d Start => _start;

    /// <summary>Конец измеряемого отрезка.</summary>
    public Vector3d End => _end;

    /// <summary>Текущая длина измеряемого отрезка, мм.</summary>
    public double CurrentLengthMm => CurrentDistance();

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
                return _state != ScaleToDimensionToolState.Idle;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры, инструмент не перехватывает
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                return Click(input.PositionPx);

            case PointerEventKind.Leave:
                _hasCursor = false;
                return _state != ScaleToDimensionToolState.Idle;

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

        if (input.Key == ToolKey.Escape)
        {
            if (_state == ScaleToDimensionToolState.Idle)
            {
                return false;
            }

            if (_state == ScaleToDimensionToolState.AwaitingValue)
            {
                _state = ScaleToDimensionToolState.StartPicked;
                SetVcb(ToolValueKind.Length, null, true);
                BuildPreview();
            }
            else
            {
                Reset();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Числовой ввод VCB (мм): после первого клика задаёт точную длину
    /// измеряемого отрезка; после выбора отрезка — целевой размер модели.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || value <= DimensionMath.MinLengthMm)
        {
            return false;
        }

        switch (_state)
        {
            case ScaleToDimensionToolState.StartPicked:
                _end = _start + MeasureDirection() * value;
                _state = ScaleToDimensionToolState.AwaitingValue;
                SetVcb(ToolValueKind.Length, CurrentDistance(), true);
                BuildPreview();
                return true;

            case ScaleToDimensionToolState.AwaitingValue:
                double current = CurrentDistance();
                if (current <= DimensionMath.MinLengthMm)
                {
                    return false;
                }

                Context.Execute(new ScaleModelCommand(Context.Scene, _start, value / current));
                Reset();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Отмена: построение сбрасывается, сцена не меняется.</summary>
    public override void Cancel() => Reset();

    private void Reset()
    {
        _state = ScaleToDimensionToolState.Idle;
        _start = Vector3d.Zero;
        _end = Vector3d.Zero;
        _hasCursor = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool Click(Vector2d positionPx)
    {
        InferenceScene scene = Context.BuildInferenceScene();
        bool hasAnchor = _state == ScaleToDimensionToolState.StartPicked;
        if (!DimensionPickSupport.TryPickPoint(Context, scene, positionPx, hasAnchor ? _start : null, out DimensionPickResult pick))
        {
            return false;
        }

        switch (_state)
        {
            case ScaleToDimensionToolState.Idle:
                if (pick.Edge is { } edge)
                {
                    // Клик по ребру без точечной привязки — измеряется ребро целиком.
                    _start = edge.Start;
                    _end = edge.End;
                    _state = ScaleToDimensionToolState.AwaitingValue;
                    SetVcb(ToolValueKind.Length, CurrentDistance(), true);
                    BuildPreview();
                    return true;
                }

                _start = pick.Point;
                _hasCursor = false;
                _state = ScaleToDimensionToolState.StartPicked;
                SetVcb(ToolValueKind.Length, null, true);
                SetPreview(ToolPreview.Empty);
                return true;

            case ScaleToDimensionToolState.StartPicked:
                _end = pick.Point;
                if (CurrentDistance() <= DimensionMath.MinLengthMm)
                {
                    return true;
                }

                _state = ScaleToDimensionToolState.AwaitingValue;
                SetVcb(ToolValueKind.Length, CurrentDistance(), true);
                BuildPreview();
                return true;

            default:
                return false;
        }
    }

    private void UpdateCursor(Vector2d positionPx)
    {
        InferenceScene scene = Context.BuildInferenceScene();
        bool hasAnchor = _state == ScaleToDimensionToolState.StartPicked;
        if (!DimensionPickSupport.TryPickPoint(Context, scene, positionPx, hasAnchor ? _start : null, out DimensionPickResult pick))
        {
            return;
        }

        _cursorPoint = pick.Point;
        _hasCursor = true;

        if (_state == ScaleToDimensionToolState.StartPicked)
        {
            SetVcb(ToolValueKind.Length, CurrentDistance(), true);
            BuildPreview();
        }
    }

    private double CurrentDistance() => _state == ScaleToDimensionToolState.StartPicked && _hasCursor
        ? (_cursorPoint - _start).Length()
        : (_end - _start).Length();

    private Vector3d MeasureDirection()
    {
        if (_hasCursor && (_cursorPoint - _start).LengthSquared() > 0)
        {
            return (_cursorPoint - _start).Normalized();
        }

        return Vector3d.UnitX;
    }

    private void BuildPreview()
    {
        if (_state == ScaleToDimensionToolState.Idle)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        Vector3d end = _state == ScaleToDimensionToolState.StartPicked && _hasCursor ? _cursorPoint : _end;
        string text = DimensionText.Format(CurrentDistance(), Context.Scene.Unit);
        SetPreview(new ToolPreview(
            [new ToolPreviewLine(_start, end, ToolPreviewStyle.Guide)],
            [new ToolPreviewPoint(_start, ToolPreviewStyle.Highlight)],
            [new ToolPreviewLabel(((_start + end) * 0.5), text)]));
    }
}
