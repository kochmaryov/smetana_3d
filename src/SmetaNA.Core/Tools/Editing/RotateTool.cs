using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Rotate (задача 2.3): вращение выбранного объекта или грани вокруг
/// нормали грани в точке базы (центроид грани — центр, точка щелчка — опорное
/// направление) либо на точный угол VCB (градусы, знак — направление по правой тройке).
/// <para>
/// В режиме грани вращаются вершины кольца и позиционно склеенные с ними вершины
/// соседних граней; в режиме объекта меняется локальная трансформация узла.
/// Изменение — одна команда истории.
/// </para>
/// </summary>
public sealed class RotateTool : TransformToolBase
{
    /// <summary>Идентификатор инструмента (команда «Rotate»).</summary>
    public const string ToolId = "tool.rotate";

    /// <summary>Порог вырожденного опорного направления (щелчок в центроиде).</summary>
    public const double ReferenceEpsilon = 1e-6;

    private Vector3d _pivot;
    private Vector3d _axis;
    private Vector3d _reference;
    private double _angleDegrees;

    public override string Id => ToolId;

    /// <summary>Текущий угол (градусы, знаковый).</summary>
    public double AngleDegrees => _angleDegrees;

    protected override ToolValueKind VcbKind => ToolValueKind.Angle;

    protected override double? CurrentVcbValue => _angleDegrees;

    protected override bool IsNoOp => Math.Abs(_angleDegrees) < 1e-9;

    protected override string PreviewText => FormatValue("{0:0.###}°", _angleDegrees);

    protected override bool TryBeginOperation(ToolFaceHit hit)
    {
        _pivot = hit.WorldTransform.TransformPoint(MeshEditOps.FaceCentroid(hit.Face));
        _axis = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        if (_axis.LengthSquared() <= 0)
        {
            return false;
        }

        _reference = EditingMath.ProjectOnPlane(hit.Point - _pivot, _axis);
        _angleDegrees = 0;
        return true;
    }

    protected override bool TryUpdateOperation(Vector2d cursorPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return false;
        }

        if (!EditingMath.TryIntersectPlane(viewport.PickRay(cursorPx), _pivot, _axis, out Vector3d hitPoint))
        {
            return false;
        }

        Vector3d direction = EditingMath.ProjectOnPlane(hitPoint - _pivot, _axis);
        if (_reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            // щелчок в центроиде: опорное направление захватывается первым движением
            if (direction.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
            {
                return false;
            }

            _reference = direction;
            return false;
        }

        if (direction.LengthSquared() <= 0)
        {
            return false;
        }

        _angleDegrees = EditingMath.SignedAngleDegrees(_reference, direction, _axis);
        WorldDelta = EditingTransforms.CreateRotation(_axis, _pivot, EditingMath.ToRadians(_angleDegrees));
        return true;
    }

    protected override bool TrySetOperationValue(double value)
    {
        if (_reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            return false;
        }

        _angleDegrees = value;
        WorldDelta = EditingTransforms.CreateRotation(_axis, _pivot, EditingMath.ToRadians(_angleDegrees));
        return true;
    }

    protected override string CommandDescription() =>
        FormatValue("Rotate {0:0.###}°", _angleDegrees);
}
