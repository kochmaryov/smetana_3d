using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Offset (задача 2.3): концентрический контур в плоскости выбранной
/// грани — мышью (перетаскивание внутрь/наружу) или числовым вводом VCB (мм,
/// положительное — инсет, отрицательное — аутсет). Грань расщепляется на кольцо
/// и внутреннюю грань одной командой истории (<see cref="OffsetFaceCommand"/>);
/// материал исходной грани наследуют все новые грани.
/// <para>
/// Во время перетаскивания меш не изменяется — рисуется предпросмотр контура
/// (<see cref="OffsetMath"/>). После фиксации цель сбрасывается: повторный навод
/// курсора берёт грань нового контура. Отмена (<c>Escape</c>/<see cref="Cancel"/>)
/// сбрасывает предпросмотр без изменения сцены.
/// </para>
/// </summary>
public sealed class OffsetTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Offset»).</summary>
    public const string ToolId = "tool.offset";

    /// <summary>Нулевое смещение не создаёт команду истории.</summary>
    public const double MinCommitDistance = 1e-9;

    /// <summary>Порог осмысленного смещения для наследования знака числового ввода.</summary>
    public const double SignEpsilon = 1e-6;

    private enum OffsetState
    {
        Idle,
        Dragging,
    }

    private OffsetState _state = OffsetState.Idle;
    private bool _hasFace;
    private MeshFaceRef _face;
    private HalfEdgeMesh _mesh = null!;
    private Transform3d _world = Transform3d.Identity;
    private Vector3d _normal;
    private IReadOnlyList<MeshVertex> _ring = [];
    private Vector3d _anchor;
    private Vector3d _inward;
    private double _distance;

    public override string Id => ToolId;

    public override bool IsInteracting => _state == OffsetState.Dragging;

    /// <summary>Текущее смещение контура (мм, со знаком: «+» — внутрь).</summary>
    public double Distance => _distance;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_state == OffsetState.Dragging)
                {
                    UpdateDragDistance(input.PositionPx);
                    return true;
                }

                UpdateHover(input.PositionPx);
                return false;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры, инструмент не перехватывает
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                return BeginDrag(input.PositionPx);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return EndDrag();

            case PointerEventKind.Leave:
                if (_state == OffsetState.Dragging)
                {
                    return true;
                }

                ClearFace();
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
            case ToolKey.Enter when _state == OffsetState.Dragging:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB (мм): во время перетаскивания знак сохраняет направление
    /// жеста, по выбранной грани (без перетаскивания) — положительное значение
    /// смещает контур внутрь, отрицательное — наружу.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        if (_state == OffsetState.Dragging)
        {
            SetDistance(_distance <= -SignEpsilon ? -Math.Abs(value) : Math.Abs(value));
            Commit();
            return true;
        }

        if (_hasFace)
        {
            SetDistance(value);
            Commit();
            return true;
        }

        return false;
    }

    /// <summary>Отмена текущего действия: предпросмотр сбрасывается, меш не меняется.</summary>
    public override void Cancel()
    {
        _state = OffsetState.Idle;
        _distance = 0;
        ClearFace();
    }

    private bool BeginDrag(Vector2d positionPx)
    {
        if (_state == OffsetState.Dragging)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearFace();
            return false;
        }

        SetFace(hit);
        _anchor = hit.Point;
        _inward = EditingMath.ProjectOnPlane(ToWorld(MeshEditOps.FaceCentroid(hit.Face)) - _anchor, _normal).Normalized();
        _distance = 0;
        _state = OffsetState.Dragging;
        SetVcb(ToolValueKind.Length, 0, true);
        UpdateDragDistance(positionPx);
        return true;
    }

    private bool EndDrag()
    {
        if (_state != OffsetState.Dragging)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        double distance = _distance;
        _state = OffsetState.Idle;
        _distance = 0;

        if (_hasFace && Math.Abs(distance) >= MinCommitDistance)
        {
            // грань заменяется новыми — цель сбрасывается, чтобы не работать
            // с устаревшей ссылкой (например, после undo)
            Context.Execute(new OffsetFaceCommand(_mesh, _face.Face, distance));
            ClearFace();
            return;
        }

        if (_hasFace)
        {
            SetVcb(ToolValueKind.Length, null, true);
            BuildHoverPreview();
        }
        else
        {
            SetVcb(ToolValueKind.None, null, false);
            SetPreview(ToolPreview.Empty);
        }
    }

    private void UpdateDragDistance(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return;
        }

        Ray3d worldRay = viewport.PickRay(positionPx);
        if (!EditingMath.TryIntersectPlane(worldRay, ToWorld(MeshEditOps.FaceCentroid(_face.Face)), _normal, out Vector3d hitPoint))
        {
            return;
        }

        double distance = _inward.LengthSquared() > 0
            ? Vector3d.Dot(hitPoint - _anchor, _inward)
            : (hitPoint - _anchor).Length();
        SetDistance(distance);
    }

    private void SetDistance(double distance)
    {
        _distance = double.IsFinite(distance) ? distance : 0;
        SetVcb(ToolValueKind.Length, _distance, true);
        BuildDragPreview();
    }

    private void UpdateHover(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearFace();
            return;
        }

        if (!_hasFace || _face.Face != hit.Face || _face.Node != hit.Node)
        {
            SetFace(hit);
        }

        SetVcb(ToolValueKind.Length, null, true);
        BuildHoverPreview();
    }

    private void SetFace(ToolFaceHit hit) => SetFace(new MeshFaceRef(hit.Node, hit.Face), hit.WorldTransform);

    private void SetFace(MeshFaceRef face) => SetFace(face, _world);

    private void SetFace(MeshFaceRef face, Transform3d world)
    {
        _face = face;
        _hasFace = true;
        _mesh = face.Node.Mesh;
        _world = world;
        _normal = world.TransformVector(MeshNormals.ComputeFaceNormal(face.Face)).Normalized();
        _ring = _mesh.GetRing(face.Face);
        Context.Selection.SetFace(_face);
    }

    private void ClearFace()
    {
        _hasFace = false;
        _face = default;
        _distance = 0;
        _ring = [];
        _anchor = default;
        _inward = default;
        if (IsActive)
        {
            Context.Selection.Clear();
        }

        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void BuildHoverPreview()
    {
        if (!_hasFace)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        AppendRingLines(lines, _ring, ToolPreviewStyle.Highlight);
        SetPreview(new ToolPreview(lines, [new ToolPreviewPoint(ToWorld(MeshEditOps.FaceCentroid(_face.Face)), ToolPreviewStyle.Highlight)]));
    }

    private void BuildDragPreview()
    {
        if (!_hasFace)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        IReadOnlyList<Vector3d> contour = OffsetMath.OffsetContour(
            [.. _ring.Select(vertex => vertex.Position)],
            MeshNormals.ComputeFaceNormal(_face.Face),
            _distance);

        var lines = new List<ToolPreviewLine>();
        AppendRingLines(lines, _ring, ToolPreviewStyle.Default);
        for (int i = 0; i < contour.Count; i++)
        {
            lines.Add(new ToolPreviewLine(
                ToWorld(contour[i]),
                ToWorld(contour[(i + 1) % contour.Count]),
                ToolPreviewStyle.Highlight));
        }

        for (int i = 0; i < _ring.Count; i++)
        {
            lines.Add(new ToolPreviewLine(
                ToWorld(_ring[i].Position),
                ToWorld(contour[i]),
                ToolPreviewStyle.Guide));
        }

        var labels = new List<ToolPreviewLabel>
        {
            new(ToWorld(MeshEditOps.FaceCentroid(_face.Face)),
                string.Create(CultureInfo.InvariantCulture, $"{_distance:0.###} mm")),
        };

        SetPreview(new ToolPreview(lines, [new ToolPreviewPoint(_anchor, ToolPreviewStyle.Default)], labels));
    }

    private void AppendRingLines(List<ToolPreviewLine> lines, IReadOnlyList<MeshVertex> ring, ToolPreviewStyle style)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            lines.Add(new ToolPreviewLine(
                ToWorld(ring[i].Position),
                ToWorld(ring[(i + 1) % ring.Count].Position),
                style));
        }
    }

    private Vector3d ToWorld(Vector3d localPoint) => _world.TransformPoint(localPoint);
}
