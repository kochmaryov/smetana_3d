using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools;

/// <summary>
/// Инструмент Push/Pull (задача 2.2): выдавливание/вдавливание выбранной грани
/// на расстояние вдоль её нормали — мышью (перетаскивание) или числовым вводом
/// VCB (подтверждение точного расстояния).
/// <para>
/// Склеивание (stickiness) выполняется в <see cref="MeshEditOps"/>: переносятся
/// вершины кольца грани и все позиционно склеенные с ними вершины соседних граней
/// и объектов того же контекста — прилегающие грани тянутся за деформируемой,
/// угол остаётся замкнутым.
/// </para>
/// <para>
/// Во время перетаскивания меш не изменяется: рисуется предпросмотр. Изменение
/// применяется одной командой истории (<see cref="MoveVerticesCommand"/>) при
/// отпускании кнопки или подтверждении значения; отмена (<c>Escape</c>/<see cref="Cancel"/>)
/// просто сбрасывает предпросмотр.
/// </para>
/// </summary>
public sealed class PushPullTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Push/Pull»).</summary>
    public const string ToolId = "tool.pushpull";

    /// <summary>Нулевое смещение не создаёт команду истории.</summary>
    public const double MinCommitDistance = 1e-9;

    /// <summary>Порог осмысленного смещения для наследования знака числового ввода.</summary>
    public const double SignEpsilon = 1e-6;

    private enum PushPullState
    {
        Idle,
        Dragging,
    }

    private PushPullState _state = PushPullState.Idle;
    private bool _hasFace;
    private MeshFaceRef _face;
    private Transform3d _world = Transform3d.Identity;
    private Vector3d _anchor;
    private Vector3d _normal;
    private IReadOnlyList<MeshVertex> _ring = [];
    private IReadOnlyList<MeshFace> _dependentFaces = [];
    private double _distance;

    public override string Id => ToolId;

    public override bool IsInteracting => _state == PushPullState.Dragging;

    /// <summary>Текущее расстояние выдавливания (мм, со знаком).</summary>
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
                if (_state == PushPullState.Dragging)
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
                if (_state == PushPullState.Dragging)
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
            case ToolKey.Enter when _state == PushPullState.Dragging:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB (мм): во время перетаскивания значение уточняет
    /// расстояние и фиксирует его (знак сохраняет направление перетаскивания);
    /// по выбранной грани — выдавливание на это расстояние.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        if (_state == PushPullState.Dragging)
        {
            // знак сохраняет направление перетаскивания (шум около нуля — наружу)
            SetDistance(_distance <= -SignEpsilon ? -Math.Abs(value) : Math.Abs(value));
            Commit();
            return true;
        }

        if (_hasFace)
        {
            SetDistance(Math.Abs(value));
            Commit();
            return true;
        }

        return false;
    }

    /// <summary>Отмена текущего действия: предпросмотр сбрасывается, меш не меняется.</summary>
    public override void Cancel()
    {
        _state = PushPullState.Idle;
        _distance = 0;
        ClearFace();
    }

    private bool BeginDrag(Vector2d positionPx)
    {
        if (_state == PushPullState.Dragging)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearFace();
            return false;
        }

        SetFace(hit);
        _anchor = hit.LocalPoint;
        _distance = 0;
        _state = PushPullState.Dragging;
        SetVcb(ToolValueKind.Length, 0, true);
        UpdateDragDistance(positionPx);
        return true;
    }

    private bool EndDrag()
    {
        if (_state != PushPullState.Dragging)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        double distance = _distance;
        _state = PushPullState.Idle;
        _distance = 0;

        if (_hasFace && Math.Abs(distance) >= MinCommitDistance)
        {
            IReadOnlyList<VertexMove> moves = MeshEditOps.PlanMove(
                _ring,
                CollectGlueScope(),
                _normal * distance,
                MeshEditOps.DefaultToleranceMm);
            string description = string.Format(
                CultureInfo.InvariantCulture, "Push/Pull {0:0.###} mm", distance);
            Context.Execute(new MoveVerticesCommand(description, moves));
        }

        // грань остаётся выбранной — можно продолжить выдавливание или ввести значение
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

        Ray3d localRay = SceneFacePicker.ToLocalRay(viewport.PickRay(positionPx), _world);
        SetDistance(PushPullMath.OffsetAlongAxis(localRay, _anchor, _normal));
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

    private void SetFace(ToolFaceHit hit)
    {
        _face = new MeshFaceRef(hit.Node, hit.Face);
        _hasFace = true;
        _world = hit.WorldTransform;
        _normal = MeshNormals.ComputeFaceNormal(hit.Face).Normalized();
        _ring = hit.Node.Mesh.GetRing(hit.Face);
        _dependentFaces = MeshEditOps.CollectDependentFaces(hit.Node.Mesh, _ring);
        Context.Selection.SetFace(_face);
    }

    private void ClearFace()
    {
        _hasFace = false;
        _face = default;
        _distance = 0;
        _ring = [];
        _dependentFaces = [];
        if (IsActive)
        {
            Context.Selection.Clear();
        }

        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    /// <summary>
    /// Область склеивания: вершины всех мешей того же контекста (одинаковая
    /// мировая трансформация), включая меш деформируемой грани.
    /// </summary>
    private IEnumerable<MeshVertex> CollectGlueScope()
    {
        var scope = new List<MeshVertex>();
        foreach (PlacedNode placed in SceneFlattener.Expand(Context.Scene))
        {
            if (placed.Node is not MeshNode meshNode || placed.WorldTransform != _world)
            {
                continue;
            }

            scope.AddRange(meshNode.Mesh.Vertices);
        }

        return scope;
    }

    private void BuildHoverPreview()
    {
        if (!_hasFace)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        AppendRingLines(lines, _ring, Vector3d.Zero, ToolPreviewStyle.Highlight);
        SetPreview(new ToolPreview(lines, [new ToolPreviewPoint(ToWorld(MeshEditOps.FaceCentroid(_face.Face)), ToolPreviewStyle.Highlight)]));
    }

    private void BuildDragPreview()
    {
        if (!_hasFace)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        Vector3d offset = _normal * _distance;
        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();

        AppendRingLines(lines, _ring, Vector3d.Zero, ToolPreviewStyle.Default);
        AppendRingLines(lines, _ring, offset, ToolPreviewStyle.Highlight);

        foreach (MeshVertex vertex in _ring)
        {
            lines.Add(new ToolPreviewLine(
                ToWorld(vertex.Position),
                ToWorld(vertex.Position + offset),
                ToolPreviewStyle.Guide));
        }

        // каскад: контуры соседних граней, которые растянутся вместе с деформируемой
        foreach (MeshFace face in _dependentFaces)
        {
            if (ReferenceEquals(face, _face.Face))
            {
                continue;
            }

            AppendRingLines(lines, FaceRing(face), offset, ToolPreviewStyle.Guide);
        }

        Vector3d anchor = ToWorld(_anchor);
        points.Add(new ToolPreviewPoint(anchor, ToolPreviewStyle.Default));
        lines.Add(new ToolPreviewLine(anchor, ToWorld(_anchor + offset), ToolPreviewStyle.Highlight));

        Vector3d labelAnchor = ToWorld(MeshEditOps.FaceCentroid(_face.Face) + offset);
        var labels = new List<ToolPreviewLabel>
        {
            new(labelAnchor, string.Format(CultureInfo.InvariantCulture, "{0:0.###} mm", _distance)),
        };

        SetPreview(new ToolPreview(lines, points, labels));
    }

    private IReadOnlyList<MeshVertex> FaceRing(MeshFace face) => _face.Node.Mesh.GetRing(face);

    private void AppendRingLines(
        List<ToolPreviewLine> lines,
        IReadOnlyList<MeshVertex> ring,
        Vector3d offset,
        ToolPreviewStyle style)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d start = ring[i].Position + offset;
            Vector3d end = ring[(i + 1) % ring.Count].Position + offset;
            lines.Add(new ToolPreviewLine(ToWorld(start), ToWorld(end), style));
        }
    }

    private Vector3d ToWorld(Vector3d localPoint) => _world.TransformPoint(localPoint);
}
