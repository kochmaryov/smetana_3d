using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools.Editing;
/// <summary>
/// Инструмент Move/Copy (задача 2.3): перенос выбранного объекта или грани
/// перетаскиванием (в плоскости экрана) либо точным расстоянием VCB (мм).
/// Ctrl при старте — копия объекта (копия уносится смещением, оригинал остаётся).
/// <para>
/// В режиме грани переносятся вершины кольца и позиционно склеенные с ними вершины
/// соседних граней (stickiness, <see cref="MeshEditOps"/>); в режиме объекта меняется
/// локальная трансформация узла. Изменение — одна команда истории
/// (<c>MoveVerticesCommand</c> / <c>SetNodeTransformCommand</c>).
/// </para>
/// </summary>
public sealed class MoveTool : TransformToolBase
{
    /// <summary>Идентификатор инструмента (команда «Move»).</summary>
    public const string ToolId = "tool.move";

    /// <summary>Порог осмысленного смещения для наследования знака числового ввода.</summary>
    public const double SignEpsilon = 1e-6;

    private Vector3d _baseWorld;
    private Vector3d _translation;
    private bool _oriented;
    private SnapFrame? _sourceFrame;

    public override string Id => ToolId;

    /// <summary>Текущее смещение (мм, мир).</summary>
    public Vector3d Translation => _translation;

    /// <summary>Доворот по соединению snap-to-snap активен (цель — чужая точка сборки).</summary>
    public bool IsOrientationAttached => _oriented;

    /// <summary>Кадр точки сборки, захваченной базой перемещения (для предпросмотра ориентации).</summary>
    public SnapFrame? SourceSnapFrame => _sourceFrame;

    protected override ToolValueKind VcbKind => ToolValueKind.Length;

    protected override double? CurrentVcbValue => _translation.Length();

    protected override bool IsNoOp => !_oriented && _translation.Length() < MinCommitDistance;

    protected override string PreviewText => FormatValue("{0:0.###} mm", _translation.Length());

    protected override bool TryBeginOperation(ToolFaceHit hit)
    {
        _baseWorld = hit.Point;
        _translation = Vector3d.Zero;
        _oriented = false;
        _sourceFrame = UserSnapFrame(CurrentSnap.Best);
        return true;
    }

    protected override bool TryUpdateOperation(Vector2d cursorPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return false;
        }

        if (!EditingMath.TryDragPlaneHit(viewport, cursorPx, _baseWorld, out Vector3d target))
        {
            return false;
        }

        _translation = target - _baseWorld;
        _oriented = false;
        WorldDelta = Transform3d.CreateTranslation(_translation);
        return true;
    }

    /// <summary>
    /// Перемещение по привязанной точке (концы, рёбра, оси, сетка). Если база и цель
    /// привязаны к пользовательским точкам сборки, объект доворачивается
    /// «лицевая к лицевой» (snap-to-snap, R-11).
    /// </summary>
    protected override bool TryUpdateOperationAt(Vector3d point)
    {
        _translation = point - _baseWorld;
        SnapFrame? targetFrame = UserSnapFrame(CurrentSnap.Best);
        if (_sourceFrame is { } source && targetFrame is { } target)
        {
            SnapFrame attached = SnapFrame.FrontToFront(source, target);
            WorldDelta = source.DeltaTo(attached);
            _oriented = true;
            return true;
        }

        _oriented = false;
        WorldDelta = Transform3d.CreateTranslation(_translation);
        return true;
    }

    /// <summary>Кадр кандидата, если привязка — пользовательская точка сборки (или комбинированная через неё).</summary>
    private static SnapFrame? UserSnapFrame(SnapCandidate? candidate) => candidate?.Frame;

    /// <summary>Ctrl во время перетаскивания превращает перенос в штамп: оригинал остаётся.</summary>
    protected override void OnModifiersChanged(ToolModifiers modifiers)
    {
        if ((modifiers & ToolModifiers.Ctrl) != 0 && !IsCopyMode)
        {
            EnableStamp();
        }
    }

    /// <summary>
    /// Точная точка из VCB: абсолютная координата — куда переместить захваченную
    /// точку, относительная — сразу смещение.
    /// </summary>
    public override bool TryApplyPoint(Vector3d point, bool relative)
    {
        if (!IsActive || !IsDragging)
        {
            return false;
        }

        _translation = relative ? point : point - _baseWorld;
        if (_translation.Length() < MinCommitDistance)
        {
            return true;
        }

        _oriented = false;
        WorldDelta = Transform3d.CreateTranslation(_translation);
        Commit();
        return true;
    }

    protected override bool TrySetOperationValue(double value)
    {
        double length = _translation.Length();
        if (length <= SignEpsilon)
        {
            // направление задаёт перетаскивание: без него точный ввод не применим
            return false;
        }

        _translation = _translation * (value / length);
        WorldDelta = Transform3d.CreateTranslation(_translation);
        return true;
    }

    protected override string CommandDescription() =>
        FormatValue("Move {0:0.###} mm", _translation.Length());
}
