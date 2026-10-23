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

    public override string Id => ToolId;

    /// <summary>Текущее смещение (мм, мир).</summary>
    public Vector3d Translation => _translation;

    protected override ToolValueKind VcbKind => ToolValueKind.Length;

    protected override double? CurrentVcbValue => _translation.Length();

    protected override bool IsNoOp => _translation.Length() < MinCommitDistance;

    protected override string PreviewText => FormatValue("{0:0.###} mm", _translation.Length());

    protected override bool TryBeginOperation(ToolFaceHit hit)
    {
        _baseWorld = hit.Point;
        _translation = Vector3d.Zero;
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
        WorldDelta = Transform3d.CreateTranslation(_translation);
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
