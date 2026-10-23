using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Soften/Smooth (задача 2.3): сглаживание рёбер между гранями —
/// щелчок/перетаскивание по граням сглаживает рёбра, у которых двугранный угол
/// не превышает порог (VCB, градусы), и прячет их (soft + smooth). Ctrl при старте
/// жеста снимает сглаживание со всех затронутых рёбер.
/// Изменение — одна команда истории (<see cref="SetEdgeSmoothCommand"/>).
/// </summary>
public sealed class SoftenSmoothTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Soften/Smooth»).</summary>
    public const string ToolId = "tool.soften";

    /// <summary>Порог двугранного угла по умолчанию, градусы.</summary>
    public const double DefaultThresholdDegrees = 20.0;

    private enum StrokeState
    {
        Idle,
        Stroking,
    }

    private StrokeState _state = StrokeState.Idle;
    private double _thresholdDegrees = DefaultThresholdDegrees;
    private bool _unsmoothStroke;
    private readonly List<(MeshNode Node, MeshFace Face)> _pending = [];
    private readonly HashSet<MeshFace> _pendingFaces = [];

    public override string Id => ToolId;

    public override bool IsInteracting => _state == StrokeState.Stroking;

    protected override void OnActivated()
    {
        SetVcb(ToolValueKind.Angle, _thresholdDegrees, true);
    }

    /// <summary>Порог сглаживания (двугранный угол, градусы).</summary>
    public double ThresholdDegrees
    {
        get => _thresholdDegrees;
        set
        {
            if (!double.IsFinite(value) || value < 0 || value > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Порог должен быть в диапазоне 0..180 градусов.");
            }

            _thresholdDegrees = value;
            SetVcb(ToolValueKind.Angle, _thresholdDegrees, true);
        }
    }

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_state == StrokeState.Stroking)
                {
                    CollectFace(input.PositionPx);
                    return true;
                }

                UpdateHover(input.PositionPx);
                return false;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры; Ctrl — жест снятия сглаживания
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                return BeginStroke(input.PositionPx, (input.Modifiers & ToolModifiers.Ctrl) != 0);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return EndStroke();

            case PointerEventKind.Leave:
                if (_state == StrokeState.Stroking)
                {
                    return true;
                }

                SetPreview(ToolPreview.Empty);
                return false;

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
            case ToolKey.Enter when _state == StrokeState.Stroking:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Числовой ввод VCB — порог сглаживания в градусах (0..180).</summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || value < 0 || value > 180)
        {
            return false;
        }

        ThresholdDegrees = value;
        return true;
    }

    /// <summary>Отмена текущего жеста: флаги рёбер не меняются.</summary>
    public override void Cancel()
    {
        _state = StrokeState.Idle;
        _unsmoothStroke = false;
        _pending.Clear();
        _pendingFaces.Clear();
        SetPreview(ToolPreview.Empty);
    }

    private bool BeginStroke(Vector2d positionPx, bool unsmooth)
    {
        if (_state == StrokeState.Stroking)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        _state = StrokeState.Stroking;
        _unsmoothStroke = unsmooth;
        _pending.Clear();
        _pendingFaces.Clear();
        Collect(hit);
        return true;
    }

    private bool EndStroke()
    {
        if (_state != StrokeState.Stroking)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        _state = StrokeState.Idle;
        IReadOnlyList<MeshHalfEdge> edges = MeshEditOps.CollectEdges(_pending.Select(entry => entry.Face));

        var toApply = new List<MeshHalfEdge>();
        foreach (MeshHalfEdge edge in edges)
        {
            bool smooth;
            bool soft;
            if (_unsmoothStroke)
            {
                smooth = false;
                soft = false;
            }
            else if (MeshEditOps.TryGetDihedralAngleDegrees(edge, out double degrees) && degrees <= _thresholdDegrees)
            {
                smooth = true;
                soft = true;
            }
            else
            {
                continue;
            }

            if (edge.IsSmooth != smooth || edge.IsSoft != soft)
            {
                toApply.Add(edge);
            }
        }

        if (toApply.Count > 0)
        {
            string description = _unsmoothStroke ? "Снятие сглаживания рёбер" : "Сглаживание рёбер";
            Context.Execute(new SetEdgeSmoothCommand(description, toApply, !_unsmoothStroke, !_unsmoothStroke));
        }

        _pending.Clear();
        _pendingFaces.Clear();
        Context.Selection.Clear();
        SetPreview(ToolPreview.Empty);
    }

    private void CollectFace(Vector2d positionPx)
    {
        if (Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            Collect(hit);
        }
    }

    private void Collect(ToolFaceHit hit)
    {
        if (!_pendingFaces.Add(hit.Face))
        {
            return;
        }

        _pending.Add((hit.Node, hit.Face));
        Context.Selection.SetFace(new MeshFaceRef(hit.Node, hit.Face));
        BuildPreview(hit.WorldTransform);
    }

    private void UpdateHover(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        BuildPreview(hit.WorldTransform);
    }

    private void BuildPreview(Transform3d world)
    {
        var lines = new List<ToolPreviewLine>();
        foreach ((MeshNode node, MeshFace face) in _pending)
        {
            IReadOnlyList<MeshVertex> ring = node.Mesh.GetRing(face);
            for (int i = 0; i < ring.Count; i++)
            {
                lines.Add(new ToolPreviewLine(
                    world.TransformPoint(ring[i].Position),
                    world.TransformPoint(ring[(i + 1) % ring.Count].Position),
                    ToolPreviewStyle.Highlight));
            }
        }

        SetPreview(new ToolPreview(lines));
    }
}
