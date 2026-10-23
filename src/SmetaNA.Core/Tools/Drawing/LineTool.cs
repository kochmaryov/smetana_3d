using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Линия» (ломаная, задача 2.1): клики ставят вершины, курсор
/// тянет очередной сегмент с привязками (концы, середины, оси, перпендикуляры),
/// VCB задаёт точную длину сегмента. Клик у первой точки замыкает контур
/// (на плоском — создаётся грань), Enter/правый клик завершают ломаную,
/// Delete убирает последнюю точку, Escape отменяет (сцена не меняется).
/// Завершённая ломаная — одна команда истории.
/// </summary>
public sealed class LineTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента (команда «Линия»).</summary>
    public const string ToolId = "tool.line";

    /// <summary>Имя создаваемого узла сцены (когда линия рисуется в пустоте).</summary>
    public const string NodeName = "Линия";

    private readonly List<Vector3d> _chain = [];
    private Vector3d? _cursor;
    private Vector3d _lastDirection = Vector3d.UnitX;

    public override string Id => ToolId;

    public override bool IsInteracting => _chain.Count > 0;

    /// <summary>Точки текущей ломаной в мировых координатах.</summary>
    public IReadOnlyList<Vector3d> Chain => _chain;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_chain.Count == 0)
                {
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
                if (_chain.Count >= 2)
                {
                    Commit(closed: false);
                }
                else
                {
                    Cancel();
                }

                return true;

            case ToolKey.Delete:
                if (_chain.Count > 1)
                {
                    _chain.RemoveAt(_chain.Count - 1);
                    _cursor = null;
                    SetVcb(ToolValueKind.Length, null, true);
                    UpdatePreview();
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB (мм): точная длина очередного сегмента в направлении
    /// курсора (если курсор не двигался — в последнем направлении). Сегмент
    /// добавляется в ломаную, цепочка продолжается.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value) || value <= 0 || _chain.Count == 0)
        {
            return false;
        }

        Vector3d origin = _chain[^1];
        Vector3d direction = _cursor is { } cursor && (cursor - origin).Length() > MinSegmentLengthMm
            ? (cursor - origin).Normalized()
            : _lastDirection;

        Vector3d next = origin + (direction * value);
        _lastDirection = direction;
        AddPoint(next);
        return true;
    }

    /// <summary>Отмена: незавершённая ломаная отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _chain.Clear();
        _cursor = null;
        _lastDirection = Vector3d.UnitX;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        if (_chain.Count == 0)
        {
            ResolveSurface(positionPx);
            AddPoint(ResolvePoint(positionPx, null, modifiers));
            return true;
        }

        if (_chain.Count >= 3 && IsNearInPixels(positionPx, _chain[0]))
        {
            Commit(closed: true);
            return true;
        }

        Vector3d next = ResolvePoint(positionPx, _chain[^1], modifiers);
        if ((next - _chain[^1]).Length() <= MinSegmentLengthMm)
        {
            return true;
        }

        _lastDirection = (next - _chain[^1]).Normalized();
        AddPoint(next);
        return true;
    }

    private bool OnRightPress()
    {
        if (_chain.Count == 0)
        {
            return false;
        }

        if (_chain.Count >= 2)
        {
            Commit(closed: false);
        }
        else
        {
            Cancel();
        }

        return true;
    }

    private void AddPoint(Vector3d point)
    {
        _chain.Add(point);
        _cursor = point;
        SetVcb(ToolValueKind.Length, null, true);
        UpdatePreview();
    }

    private void UpdateCursor(Vector2d positionPx, ToolModifiers modifiers)
    {
        Vector3d point = ResolvePoint(positionPx, _chain[^1], modifiers);
        _cursor = point;
        double length = (point - _chain[^1]).Length();
        if (length > MinSegmentLengthMm)
        {
            _lastDirection = (point - _chain[^1]) * (1.0 / length);
        }

        SetVcb(ToolValueKind.Length, length, true);
        UpdatePreview();
    }

    private void Commit(bool closed)
    {
        var points = new List<Vector3d>(_chain);
        bool createFace = closed && DrawingShapes.IsPlanar(points);
        string description = closed ? "Линия (замкнутая)" : "Линия";
        DrawingShapeSpec spec = closed
            ? DrawingShapeSpec.Closed(points, createFace)
            : DrawingShapeSpec.Open(points);

        CommitShape(description, NodeName, spec);
        _chain.Clear();
        _cursor = null;
        ResetSurface();
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void UpdatePreview()
    {
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        AppendLines(lines, _chain, ToolPreviewStyle.Default);
        foreach (Vector3d vertex in _chain)
        {
            points.Add(new ToolPreviewPoint(vertex, ToolPreviewStyle.Default));
        }

        if (_chain.Count > 0 && _cursor is { } cursor)
        {
            lines.Add(new ToolPreviewLine(_chain[^1], cursor, ToolPreviewStyle.Highlight));
            points.Add(new ToolPreviewPoint(cursor, ToolPreviewStyle.Highlight));
            labels.Add(LabelAt(cursor, FormatMm((cursor - _chain[^1]).Length())));
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
