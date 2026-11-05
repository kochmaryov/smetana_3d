using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Инструмент «Точка сборки» (Snap, W5): первый клик ставит центр диска — с
/// привязками к вершинам, рёбрам, граням и другим snap'ам, второй клик задаёт
/// ориентацию (нормаль диска) по направлению от центра к курсору. Enter
/// завершает с ориентацией по умолчанию (синяя ось), Esc отменяет шаг.
/// Snap добавляется в контейнер грани под курсором (иначе — в корень сцены)
/// одной командой истории.
/// </summary>
public sealed class SnapTool : DrawingToolBase
{
    /// <summary>Идентификатор инструмента.</summary>
    public const string ToolId = "tool.snap";

    /// <summary>Имя snap-узла по умолчанию.</summary>
    public const string NodeName = "Точка сборки";

    /// <summary>Радиус диска на экране, пикселей (превью и маркер).</summary>
    public const double DiskRadiusPx = 14.0;

    /// <summary>Сегментов в окружности диска.</summary>
    private const int DiskSegments = 24;

    private Vector3d? _position;
    private SceneNode? _parent;
    private Vector3d? _cursor;
    private SnapNode? _editing;

    public override string Id => ToolId;

    public override bool IsInteracting => _position is not null;

    /// <summary>Центр диска, выбранный первым кликом (<c>null</c> — шаг не начат).</summary>
    public Vector3d? Position => _position;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                _cursor = ResolvePoint(input.PositionPx, _position, input.Modifiers);
                UpdatePreview();
                return true;

            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return OnLeftPress(input.PositionPx, input.Modifiers);

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
                if (_position is not null)
                {
                    ResetStep();
                    return true;
                }

                Cancel();
                return true;

            case ToolKey.Enter:
                if (_position is { } center)
                {
                    FinishStep(center, DefaultDirection);
                    ResetStep();
                    return true;
                }

                return false;

            case ToolKey.Delete:
                return DeleteHoveredSnap();

            default:
                return false;
        }
    }

    public override bool TryApplyValue(double value) => false;

    public override void Cancel() => ResetStep();

    /// <summary>Нормаль диска по умолчанию — синяя ось мира.</summary>
    public static Vector3d DefaultDirection => Vector3d.UnitZ;

    /// <summary>Snap, ориентация которого правится (<c>null</c> — создаётся новый).</summary>
    public SnapNode? EditingSnap => _editing;

    private bool OnLeftPress(Vector2d positionPx, ToolModifiers modifiers)
    {
        Vector3d point = ResolvePoint(positionPx, _position, modifiers);
        if (_position is not { } center)
        {
            if (TryBeginEdit())
            {
                return true;
            }

            _position = point;
            _parent = ResolveParent(positionPx);
            _cursor = point;
            UpdatePreview();
            return true;
        }

        // Alt при выборе ориентации переворачивает диск (как в режиме правки Snap'ов).
        Vector3d direction = point - center;
        if ((modifiers & ToolModifiers.Alt) != 0)
        {
            direction = -direction;
        }

        FinishStep(
            center,
            direction.LengthSquared() > MinSegmentLengthMm * MinSegmentLengthMm
                ? direction
                : DefaultDirection);
        ResetStep();
        return true;
    }

    /// <summary>
    /// Клик по существующему snap'у не создаёт новый диск, а начинает правку его
    /// ориентации: позиция берётся из узла, второй клик применяет кадр с undo.
    /// </summary>
    private bool TryBeginEdit()
    {
        if (CurrentSnap.Best?.Entity is not { Kind: SnapEntityKind.UserSnap, NodeId: { } nodeId })
        {
            return false;
        }

        if (FindSnap(nodeId) is not { } snap)
        {
            return false;
        }

        _editing = snap;
        _position = snap.WorldFrame(snap.WorldTransform()).Position;
        _parent = snap.Parent;
        _cursor = _position;
        UpdatePreview();
        return true;
    }

    private void FinishStep(Vector3d center, Vector3d direction)
    {
        if (_editing is { } snap)
        {
            ApplyEditedFrame(snap, center, direction);
            return;
        }

        AddSnap(center, direction);
    }

    private void ApplyEditedFrame(SnapNode snap, Vector3d center, Vector3d direction)
    {
        Vector3d up = Math.Abs(Vector3d.Dot(direction.Normalized(), Vector3d.UnitZ)) > 0.999
            ? Vector3d.UnitY
            : Vector3d.UnitZ;

        if (!SnapFrame.TryCreate(center, direction, up, out SnapFrame worldFrame)
            || !TransformInverter.TryInvert(snap.WorldTransform(), out Transform3d worldToLocal))
        {
            return;
        }

        Context.Execute(new SetSnapFrameCommand(snap, worldFrame.Transformed(worldToLocal)));
    }

    /// <summary>Удалить snap под курсором (одна запись истории); <c>false</c> — под курсором не snap.</summary>
    private bool DeleteHoveredSnap()
    {
        if (_position is not null
            || CurrentSnap.Best?.Entity is not { Kind: SnapEntityKind.UserSnap, NodeId: { } nodeId }
            || FindSnap(nodeId) is not { } snap)
        {
            return false;
        }

        Context.Execute(new RemoveNodeCommand(snap));
        SetSnap(SnapResult.Empty);
        return true;
    }

    private SnapNode? FindSnap(Guid nodeId)
        => SnapQuery.FindInScene(Context.Scene, includeHidden: true)
            .FirstOrDefault(snap => snap.Id == nodeId);

    /// <summary>Контейнер нового snap'а: родитель узла грани под курсором или корень сцены.</summary>
    private SceneNode ResolveParent(Vector2d positionPx)
        => Context.TryPickFace(positionPx, out ToolFaceHit hit) && hit.Node.Parent is { } parent
            ? parent
            : Context.Scene.Root;

    private void AddSnap(Vector3d center, Vector3d direction)
    {
        Vector3d up = Math.Abs(Vector3d.Dot(direction.Normalized(), Vector3d.UnitZ)) > 0.999
            ? Vector3d.UnitY
            : Vector3d.UnitZ;

        if (!SnapFrame.TryCreate(center, direction, up, out SnapFrame frame))
        {
            frame = new SnapFrame(center, DefaultDirection, Vector3d.UnitY);
        }

        var snap = new SnapNode { Name = NodeName, Frame = frame };
        Context.Execute(new AddNodeCommand(_parent ?? Context.Scene.Root, snap));
    }

    private void ResetStep()
    {
        _position = null;
        _parent = null;
        _cursor = null;
        _editing = null;
        SetPreview(ToolPreview.Empty);
    }

    private void UpdatePreview()
    {
        if (Context.Viewport is not { } viewport)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        Vector3d center = _position ?? _cursor ?? Vector3d.Zero;
        Vector3d direction = _position is { } anchor && _cursor is { } cursor
            && (cursor - anchor).LengthSquared() > MinSegmentLengthMm * MinSegmentLengthMm
                ? cursor - anchor
                : DefaultDirection;

        Vector3d up = Math.Abs(Vector3d.Dot(direction.Normalized(), Vector3d.UnitZ)) > 0.999
            ? Vector3d.UnitY
            : Vector3d.UnitZ;

        SnapFrame frame = SnapFrame.TryCreate(center, direction, up, out SnapFrame created)
            ? created
            : new SnapFrame(center, DefaultDirection, Vector3d.UnitY);

        double radius = viewport.WorldRadiusAt(center, DiskRadiusPx);
        var lines = new List<ToolPreviewLine>(DiskSegments);
        for (int i = 0; i < DiskSegments; i++)
        {
            double a0 = 2 * Math.PI * i / DiskSegments;
            double a1 = 2 * Math.PI * (i + 1) / DiskSegments;
            lines.Add(new ToolPreviewLine(
                CirclePoint(frame, radius, a0),
                CirclePoint(frame, radius, a1),
                ToolPreviewStyle.Guide));
        }

        var points = new List<ToolPreviewPoint> { new(center, ToolPreviewStyle.Highlight) };
        if (_position is not null)
        {
            // нормаль диска: короткая линия от центра к курсору
            points.Add(new ToolPreviewPoint(center + frame.Direction * radius, ToolPreviewStyle.Highlight));
            lines.Add(new ToolPreviewLine(center, center + frame.Direction * radius, ToolPreviewStyle.Default));
        }

        SetPreview(new ToolPreview(lines, points, []));
    }

    private static Vector3d CirclePoint(SnapFrame frame, double radius, double angle)
        => frame.Position
            + frame.Right * (Math.Cos(angle) * radius)
            + frame.Up * (Math.Sin(angle) * radius);
}
