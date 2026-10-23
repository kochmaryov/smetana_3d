using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.M6;

/// <summary>Способ задания плоскости симметрии (зеркала).</summary>
public enum MirrorPlaneMode
{
    /// <summary>Относительно оси: плоскость через точку клика, перпендикулярная
    /// главной оси нормали грани (Flip Along X/Y/Z).</summary>
    Axis,

    /// <summary>Относительно двух точек: вертикальная плоскость, проходящая
    /// через обе точки (для вертикального отрезка — плоскость «лицом к камере»).</summary>
    TwoPoints,

    /// <summary>Грань как плоскость: плоскость выбранной грани.</summary>
    Face,
}

/// <summary>
/// Зеркало/симметрия (задача 5.1): отражение выбранного объекта относительно
/// плоскости, заданной осью, двумя точками или гранью
/// (<see cref="MirrorPlaneMode"/>). Ctrl при фиксирующем клике — режим копии
/// (оригинал остаётся на месте); без Ctrl объект отражается «на место».
/// <para>
/// Результат — зеркальная копия узла (<see cref="M6NodeCloner.DeepMirror"/>):
/// геометрия перестраивается с реверсом обхода граней (наружные нормали
/// сохраняются), инстансы получают отдельное «зеркальное» определение.
/// Операция — одна составная команда истории (одна отмена возвращает
/// исходное состояние).
/// </para>
/// </summary>
public sealed class MirrorTool : M6ToolBase
{
    /// <summary>Идентификатор команды «Зеркало (ось)».</summary>
    public const string AxisToolId = "tool.mirror.axis";

    /// <summary>Идентификатор команды «Зеркало (2 точки)».</summary>
    public const string TwoPointsToolId = "tool.mirror.points";

    /// <summary>Идентификатор команды «Зеркало (грань)».</summary>
    public const string FaceToolId = "tool.mirror.face";

    /// <summary>Минимальное расстояние между точками плоскости «2 точки», мм.</summary>
    public const double MinPlanePointsDistanceMm = 1e-6;

    private readonly MirrorPlaneMode _mode;
    private Vector3d _firstPoint;
    private bool _hasFirstPoint;
    private SceneNode? _subject;

    public MirrorTool(MirrorPlaneMode mode)
    {
        _mode = mode;
    }

    public override string Id => _mode switch
    {
        MirrorPlaneMode.Axis => AxisToolId,
        MirrorPlaneMode.TwoPoints => TwoPointsToolId,
        MirrorPlaneMode.Face => FaceToolId,
        _ => throw new InvalidOperationException("Неизвестный режим задания плоскости зеркала."),
    };

    /// <summary>Способ задания плоскости симметрии.</summary>
    public MirrorPlaneMode Mode => _mode;

    /// <summary>Идёт задание второй точки (режим «2 точки»).</summary>
    public override bool IsInteracting => _mode == MirrorPlaneMode.TwoPoints && _hasFirstPoint;

    /// <summary>Первая точка плоскости (режим «2 точки»).</summary>
    public Vector3d FirstPoint => _firstPoint;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                UpdatePreview(input.PositionPx);
                return _hasFirstPoint;

            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return OnLeftPress(input.PositionPx, (input.Modifiers & ToolModifiers.Ctrl) != 0);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return _hasFirstPoint;

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
            Cancel();
            return true;
        }

        return false;
    }

    /// <summary>Зеркало числом не управляется (опция «копия» — Ctrl).</summary>
    public override bool TryApplyValue(double value) => false;

    /// <summary>Отмена: незавершённое задание плоскости отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _hasFirstPoint = false;
        _firstPoint = Vector3d.Zero;
        _subject = null;
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx, bool copyRequested)
    {
        if (_mode == MirrorPlaneMode.TwoPoints && _hasFirstPoint)
        {
            return FinishTwoPoints(positionPx, copyRequested);
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            if (_mode == MirrorPlaneMode.TwoPoints)
            {
                return TryBeginTwoPoints(positionPx);
            }

            return false;
        }

        Vector3d normal = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        if (normal.LengthSquared() <= 0)
        {
            return false;
        }

        if (_mode == MirrorPlaneMode.TwoPoints)
        {
            _subject = ResolveSubject(hit);
            _firstPoint = hit.Point;
            _hasFirstPoint = true;
            UpdatePreview(positionPx);
            return true;
        }

        Vector3d planeNormal = _mode == MirrorPlaneMode.Axis ? DominantAxis(normal) : normal;
        return Commit(ResolveSubject(hit), hit.Point, planeNormal, copyRequested);
    }

    private bool TryBeginTwoPoints(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport
            || !M6Transforms.TryPlaneHit(viewport.PickRay(positionPx), Vector3d.Zero, Vector3d.UnitZ, out Vector3d point))
        {
            return false;
        }

        _subject = SelectionCandidate;
        _firstPoint = point;
        _hasFirstPoint = true;
        UpdatePreview(positionPx);
        return true;
    }

    private bool FinishTwoPoints(Vector2d positionPx, bool copyRequested)
    {
        Vector3d second;
        if (Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            second = hit.Point;
        }
        else if (Context.Viewport is { } viewport
            && M6Transforms.TryPlaneHit(viewport.PickRay(positionPx), Vector3d.Zero, Vector3d.UnitZ, out Vector3d ground))
        {
            second = ground;
        }
        else
        {
            return true;
        }

        Vector3d segment = second - _firstPoint;
        if (segment.Length() < MinPlanePointsDistanceMm)
        {
            return true;
        }

        Vector3d normal = Vector3d.Cross(Vector3d.UnitZ, segment);
        if (normal.LengthSquared() <= MinPlanePointsDistanceMm * MinPlanePointsDistanceMm)
        {
            // отрезок вертикален: плоскость «лицом к камере» через обе точки
            normal = Context.Viewport is { } viewport
                ? Editing.EditingMath.ProjectOnPlane(viewport.Forward, segment)
                : Vector3d.UnitX;
        }

        if (normal.LengthSquared() <= 0)
        {
            return true;
        }

        if (_subject is not { } subject || subject.Parent is null)
        {
            // субъект не определён первым кликом — плоскость не применяется
            _hasFirstPoint = false;
            return true;
        }

        bool applied = Commit(subject, _firstPoint, normal.Normalized(), copyRequested);
        _hasFirstPoint = false;
        _subject = null;
        return applied;
    }

    private bool Commit(SceneNode subject, Vector3d planePoint, Vector3d planeNormal, bool copy)
    {
        if (subject.Parent is not { } parent)
        {
            return false;
        }

        Transform3d mirror = M6Transforms.CreateReflection(planePoint, planeNormal);
        SceneNode twin = M6NodeCloner.DeepMirror(subject, mirror);
        var commands = new List<IEditCommand>();
        if (copy)
        {
            commands.Add(new AddNodeCommand(parent, twin));
            Context.Execute(new CompositeEditCommand("Зеркало (копия)", commands));
        }
        else
        {
            int index = parent.IndexOfChild(subject);
            commands.Add(new AddNodeCommand(parent, twin, index));
            commands.Add(new RemoveNodeCommand(subject));
            Context.Execute(new CompositeEditCommand("Зеркало", commands));
            if (ReferenceEquals(Context.Selection.Node, subject))
            {
                Context.Selection.SetNode(twin);
            }
        }

        return true;
    }

    private static Vector3d DominantAxis(Vector3d normal)
    {
        double x = Math.Abs(normal.X);
        double y = Math.Abs(normal.Y);
        double z = Math.Abs(normal.Z);
        if (x >= y && x >= z)
        {
            return new Vector3d(Math.Sign(normal.X), 0, 0);
        }

        return y >= z
            ? new Vector3d(0, Math.Sign(normal.Y), 0)
            : new Vector3d(0, 0, Math.Sign(normal.Z));
    }

    private void UpdatePreview(Vector2d positionPx)
    {
        if (_mode == MirrorPlaneMode.TwoPoints && _hasFirstPoint)
        {
            Vector3d second = _firstPoint;
            if (Context.TryPickFace(positionPx, out ToolFaceHit hit))
            {
                second = hit.Point;
            }
            else if (Context.Viewport is { } viewport
                && M6Transforms.TryPlaneHit(viewport.PickRay(positionPx), Vector3d.Zero, Vector3d.UnitZ, out Vector3d ground))
            {
                second = ground;
            }

            SetPreview(new ToolPreview(
                [new ToolPreviewLine(_firstPoint, second, ToolPreviewStyle.Highlight)],
                [new ToolPreviewPoint(_firstPoint, ToolPreviewStyle.Default)]));
            return;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hover))
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        Vector3d normal = hover.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hover.Face)).Normalized();
        Vector3d planeNormal = _mode == MirrorPlaneMode.Axis ? DominantAxis(normal) : normal;
        if (_mode == MirrorPlaneMode.TwoPoints)
        {
            planeNormal = Vector3d.UnitZ;
        }

        var lines = new List<ToolPreviewLine>();
        AppendPlane(
            lines,
            hover.Point,
            planeNormal,
            Math.Max(20, BoundingBox3d.FromMesh(hover.Node.Mesh).DiagonalLength * 0.25),
            ToolPreviewStyle.Highlight,
            Context.Viewport);
        SetPreview(new ToolPreview(lines, [new ToolPreviewPoint(hover.Point, ToolPreviewStyle.Default)]));
    }
}
