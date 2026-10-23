using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Scale (задача 2.3): равномерное масштабирование выбранного объекта
/// или грани вокруг центроида грани (радиус от центра до курсора относительно
/// опорного — множитель) либо на точный множитель VCB (> 0).
/// <para>
/// В режиме грани масштабируются вершины кольца и позиционно склеенные с ними
/// вершины соседних граней; в режиме объекта меняется локальная трансформация узла.
/// Изменение — одна команда истории.
/// </para>
/// </summary>
public sealed class ScaleTool : TransformToolBase
{
    /// <summary>Идентификатор инструмента (команда «Scale»).</summary>
    public const string ToolId = "tool.scale";

    /// <summary>Порог вырожденного опорного радиуса (щелчок в центроиде).</summary>
    public const double ReferenceEpsilon = 1e-6;

    private Vector3d _pivot;
    private Vector3d _axis;
    private double _referenceDistance;
    private double _factor = 1;

    public override string Id => ToolId;

    /// <summary>Текущий множитель масштаба.</summary>
    public double Factor => _factor;

    protected override ToolValueKind VcbKind => ToolValueKind.Coordinate;

    protected override double? CurrentVcbValue => _factor;

    protected override bool IsNoOp => Math.Abs(_factor - 1.0) < 1e-9;

    protected override string PreviewText => FormatValue("×{0:0.###}", _factor);

    protected override bool TryBeginOperation(ToolFaceHit hit)
    {
        _pivot = hit.WorldTransform.TransformPoint(MeshEditOps.FaceCentroid(hit.Face));
        _axis = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        if (_axis.LengthSquared() <= 0)
        {
            return false;
        }

        _referenceDistance = EditingMath.ProjectOnPlane(hit.Point - _pivot, _axis).Length();
        _factor = 1;
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

        double distance = EditingMath.ProjectOnPlane(hitPoint - _pivot, _axis).Length();
        if (_referenceDistance <= ReferenceEpsilon)
        {
            // щелчок в центроиде: опорный радиус захватывается первым движением
            if (distance <= ReferenceEpsilon)
            {
                return false;
            }

            _referenceDistance = distance;
            return false;
        }

        _factor = distance / _referenceDistance;
        WorldDelta = EditingTransforms.CreateUniformScale(_pivot, _factor);
        return true;
    }

    protected override bool TrySetOperationValue(double value)
    {
        if (_referenceDistance <= ReferenceEpsilon || !double.IsFinite(value) || value <= 0)
        {
            return false;
        }

        _factor = value;
        WorldDelta = EditingTransforms.CreateUniformScale(_pivot, _factor);
        return true;
    }

    protected override string CommandDescription() =>
        FormatValue("Scale ×{0:0.###}", _factor);
}
