using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// База инструментов трансформации (Move/Rotate/Scale): перетаскивание от базовой
/// точки курсором либо точное значение VCB; изменение применяется одной командой
/// истории при отпускании/подтверждении, отмена (<c>Escape</c>/<see cref="Cancel"/>)
/// просто сбрасывает предпросмотр.
/// <para>
/// Субъект операции: если к моменту активации инструмента выбран узел сцены
/// (<c>ToolContext.Selection.Node</c>) и жест начинается в его поддереве —
/// трансформируется объект (локальная трансформация узла, Ctrl при старте — копия
/// объекта); иначе — грань под курсором: её кольцо и позиционно склеенные вершины
/// соседних граней (stickiness, как в Push/Pull).
/// </para>
/// </summary>
public abstract class TransformToolBase : ToolBase
{
    /// <summary>Нулевая трансформация не создаёт команду истории.</summary>
    public const double MinCommitDistance = 1e-9;

    private enum DragState
    {
        Idle,
        Dragging,
    }

    private DragState _state = DragState.Idle;
    private bool _hasHit;
    private ToolFaceHit _hit;
    private MeshFaceRef _faceRef;
    private Transform3d _world = Transform3d.Identity;
    private IReadOnlyList<MeshVertex> _ring = [];

    private SceneNode? _selectionCandidate;
    private SceneNode? _subjectNode;
    private Transform3d _subjectLocal = Transform3d.Identity;
    private Transform3d _subjectWorld = Transform3d.Identity;
    private bool _copyMode;
    private SceneNode? _copy;

    /// <summary>Текущее мировое преобразование операции (заполняется наследниками).</summary>
    public Transform3d WorldDelta { get; protected set; } = Transform3d.Identity;

    public override bool IsInteracting => _state == DragState.Dragging;

    /// <summary>Режим объекта (трансформация узла), иначе — геометрия грани.</summary>
    public bool IsObjectMode => _subjectNode is not null;

    /// <summary>Режим копии объекта (Ctrl при старте перетаскивания).</summary>
    public bool IsCopyMode => _copyMode;

    /// <summary>Тип значения VCB инструмента.</summary>
    protected abstract ToolValueKind VcbKind { get; }

    /// <summary>Текущее значение для VCB (мм/градусы/множитель) или <c>null</c>.</summary>
    protected abstract double? CurrentVcbValue { get; }

    /// <summary>Операция не меняет сцену (нулевое смещение, нулевой угол, масштаб 1).</summary>
    protected abstract bool IsNoOp { get; }

    /// <summary>Текст подписи предпросмотра (значение с единицей).</summary>
    protected abstract string PreviewText { get; }

    /// <summary>Зафиксировать опорные данные операции по точке базы (world); <c>false</c> — вырождено.</summary>
    protected abstract bool TryBeginOperation(ToolFaceHit hit);

    /// <summary>Обновить операцию по курсору (пиксели); <c>false</c> — курсор вне рабочей плоскости.</summary>
    protected abstract bool TryUpdateOperation(Vector2d cursorPx);

    /// <summary>Установить операцию по точному значению VCB; <c>false</c> — значение не применимо.</summary>
    protected abstract bool TrySetOperationValue(double value);

    /// <summary>Описание команды истории (например, «Move 12.5 mm»).</summary>
    protected abstract string CommandDescription();

    /// <summary>
    /// Объектный режим: при активации фиксируется выбранный узел сцены
    /// (Selection.Node) — жесты в его поддереве трансформируют объект целиком.
    /// </summary>
    protected override void OnActivated()
    {
        _selectionCandidate = Context.Selection.Node;
    }

    protected override void OnDeactivated()
    {
        _selectionCandidate = null;
    }

    /// <summary>Мировая точка базы операции (для предпросмотра).</summary>
    protected Vector3d BasePoint { get; private set; }

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_state == DragState.Dragging)
                {
                    UpdateDrag(input.PositionPx);
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

                return BeginDrag(input.PositionPx, (input.Modifiers & ToolModifiers.Ctrl) != 0);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return EndDrag();

            case PointerEventKind.Leave:
                if (_state == DragState.Dragging)
                {
                    return true;
                }

                ClearHit();
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
            case ToolKey.Enter when _state == DragState.Dragging:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Точное значение VCB: применяется во время перетаскивания и фиксирует операцию.</summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || _state != DragState.Dragging || !double.IsFinite(value))
        {
            return false;
        }

        if (!TrySetOperationValue(value))
        {
            return false;
        }

        Commit();
        return true;
    }

    /// <summary>Отмена текущего действия: предпросмотр сбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _state = DragState.Idle;
        _copy = null;
        _copyMode = false;
        ClearHit();
    }

    private bool BeginDrag(Vector2d positionPx, bool copyRequested)
    {
        if (_state == DragState.Dragging)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearHit();
            return false;
        }

        SetHit(hit);

        if (_selectionCandidate is { } selected && IsSelfOrAncestor(selected, hit.Node))
        {
            _subjectNode = selected;
            _subjectWorld = EditingMath.WorldTransformOf(selected);
            _subjectLocal = selected.LocalTransform;
            _copyMode = copyRequested && selected.Parent is not null;
            _copy = _copyMode ? NodeCloner.DeepCopy(selected) : null;
        }

        BasePoint = hit.Point;
        if (!TryBeginOperation(hit))
        {
            _subjectNode = null;
            _copy = null;
            _copyMode = false;
            return false;
        }

        WorldDelta = Transform3d.Identity;
        _state = DragState.Dragging;
        SetVcb(VcbKind, CurrentVcbValue, true);
        UpdateDrag(positionPx);
        return true;
    }

    private bool EndDrag()
    {
        if (_state != DragState.Dragging)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        Transform3d delta = WorldDelta;
        bool apply = !IsNoOp && HasGeometryChange(delta);
        _state = DragState.Idle;

        if (apply)
        {
            if (_subjectNode is { } node)
            {
                CommitNode(node, delta);
            }
            else if (_hasHit)
            {
                CommitFace(delta);
            }
        }

        _copy = null;
        _copyMode = false;
        _subjectNode = null;
        WorldDelta = Transform3d.Identity;
        SetVcb(VcbKind, null, _hasHit);
        BuildPreview();
    }

    private void CommitNode(SceneNode node, Transform3d delta)
    {
        if (!EditingTransforms.TryComposeWorldDelta(node.LocalTransform, _subjectWorld, delta, out Transform3d newLocal))
        {
            return;
        }

        if (_copy is { } copy && _copyMode)
        {
            if (node.Parent is not { } parent)
            {
                return;
            }

            Context.Execute(new CompositeEditCommand(CommandDescription(),
            [
                new AddNodeCommand(parent, copy),
                new SetNodeTransformCommand(copy, newLocal),
            ]));
        }
        else
        {
            Context.Execute(new SetNodeTransformCommand(node, newLocal));
        }
    }

    private void CommitFace(Transform3d delta)
    {
        if (!EditingTransforms.TryConjugatedMap(_world, delta, out Func<Vector3d, Vector3d> map))
        {
            return;
        }

        IReadOnlyList<VertexMove> moves = MeshEditOps.PlanTransform(
            _ring,
            CollectGlueScope(),
            map,
            MeshEditOps.DefaultToleranceMm);
        Context.Execute(new MoveVerticesCommand(CommandDescription(), moves));
    }

    private void UpdateDrag(Vector2d positionPx)
    {
        if (!TryUpdateOperation(positionPx))
        {
            return;
        }

        SetVcb(VcbKind, CurrentVcbValue, true);
        BuildPreview();
    }

    private void UpdateHover(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearHit();
            return;
        }

        if (!_hasHit || _faceRef.Face != hit.Face || _faceRef.Node != hit.Node)
        {
            SetHit(hit);
        }

        SetVcb(VcbKind, null, true);
        BuildPreview();
    }

    private void SetHit(ToolFaceHit hit)
    {
        _hit = hit;
        _hasHit = true;
        _faceRef = new MeshFaceRef(hit.Node, hit.Face);
        _world = hit.WorldTransform;
        _ring = hit.Node.Mesh.GetRing(hit.Face);
        BasePoint = hit.Point;

        if (_selectionCandidate is { } selected && IsSelfOrAncestor(selected, hit.Node))
        {
            // объектный режим: выборка остаётся на выбранном объекте
            Context.Selection.SetNode(selected);
        }
        else
        {
            Context.Selection.SetFace(_faceRef);
        }
    }

    private void ClearHit()
    {
        _hasHit = false;
        _hit = default;
        _faceRef = default;
        _ring = [];
        _subjectNode = null;
        _copy = null;
        _copyMode = false;
        if (IsActive)
        {
            Context.Selection.Clear();
        }

        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private void BuildPreview()
    {
        if (!_hasHit)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        AppendRingLines(lines, _ring, Transform3d.Identity, ToolPreviewStyle.Default);
        AppendRingLines(lines, _ring, WorldDelta, ToolPreviewStyle.Highlight);

        foreach (MeshVertex vertex in _ring)
        {
            Vector3d start = ToWorld(vertex.Position);
            Vector3d end = WorldDelta.TransformPoint(start);
            lines.Add(new ToolPreviewLine(start, end, ToolPreviewStyle.Guide));
        }

        points.Add(new ToolPreviewPoint(BasePoint, ToolPreviewStyle.Default));
        if (_state == DragState.Dragging)
        {
            var labels = new List<ToolPreviewLabel>
            {
                new(WorldDelta.TransformPoint(ToWorld(MeshEditOps.FaceCentroid(_hit.Face))), PreviewText),
            };

            SetPreview(new ToolPreview(lines, points, labels));
            return;
        }

        SetPreview(new ToolPreview(lines, points));
    }

    private void AppendRingLines(
        List<ToolPreviewLine> lines,
        IReadOnlyList<MeshVertex> ring,
        Transform3d delta,
        ToolPreviewStyle style)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d start = delta.TransformPoint(ToWorld(ring[i].Position));
            Vector3d end = delta.TransformPoint(ToWorld(ring[(i + 1) % ring.Count].Position));
            lines.Add(new ToolPreviewLine(start, end, style));
        }
    }

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

    private static bool IsSelfOrAncestor(SceneNode candidate, SceneNode node)
    {
        for (SceneNode? current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasGeometryChange(Transform3d delta)
    {
        return delta != Transform3d.Identity;
    }

    private Vector3d ToWorld(Vector3d localPoint) => _world.TransformPoint(localPoint);

    /// <summary>Локализуемое описание значения: число с единицей измерения.</summary>
    protected static string FormatValue(string format, double value) =>
        string.Format(CultureInfo.InvariantCulture, format, value);
}
