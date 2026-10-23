using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Круговой массив (задача 5.1): копии выбранного объекта через равные углы
/// вокруг оси (нормаль грани в точке клика, как у Rotate).
/// <para>
/// Сценарий: клик по грани объекта — центр и ось массива; перетаскивание
/// задаёт угловой шаг (VCB — точный угол,°; первое движение задаёт опорное
/// направление); отпускание (или значение VCB) фиксирует шаг и переходит к
/// числу копий (VCB — количество, ввод фиксирует массив). Enter/клик фиксируют
/// массив с текущими параметрами, Escape отменяет без изменений.
/// </para>
/// <para>
/// Результат — копии узлов (<see cref="M6NodeCloner.DeepCopy"/>, инстансы
/// компонентов — как инстансы того же определения); массив — одна составная
/// команда истории: одна отмена убирает все копии.
/// </para>
/// </summary>
public sealed class PolarArrayTool : M6ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Массив (круговой)»).</summary>
    public const string ToolId = "tool.array.polar";

    /// <summary>Порог вырожденного опорного направления (щелчок в центре).</summary>
    public const double ReferenceEpsilon = 1e-6;

    private enum Stage
    {
        Idle,
        SettingStep,
        SettingCount,
    }

    private Stage _stage = Stage.Idle;
    private SceneNode? _subject;
    private Vector3d _pivot;
    private Vector3d _axis;
    private Vector3d _reference;
    private double _angleDegrees;
    private int _count = ArrayMath.DefaultCount;
    private bool _swallowRelease;

    public override string Id => ToolId;

    public override bool IsInteracting => _stage == Stage.SettingStep;

    /// <summary>Центр и ось массива (мировые координаты).</summary>
    public (Vector3d Pivot, Vector3d Axis) Rotation => (_pivot, _axis);

    /// <summary>Текущий угловой шаг (градусы, знаковый).</summary>
    public double AngleDegrees => _angleDegrees;

    /// <summary>Число копий (без исходного объекта).</summary>
    public int Count => _count;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_stage != Stage.SettingStep)
                {
                    return false;
                }

                UpdateAngle(input.PositionPx);
                return true;

            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return OnLeftPress(input.PositionPx);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return OnLeftRelease();

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
                if (_stage == Stage.SettingCount || (_stage == Stage.SettingStep && HasAngle))
                {
                    Commit();
                }
                else if (_stage != Stage.Idle)
                {
                    Cancel();
                }
                else
                {
                    return false;
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Числовой ввод VCB: в фазе шага — точный угол (°, направление задаёт
    /// опорное направление перетаскивания), в фазе количества — число копий,
    /// фиксирующее массив.
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        switch (_stage)
        {
            case Stage.SettingStep:
                if (_reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon
                    || Math.Abs(value) < ArrayMath.MinStepDegrees)
                {
                    return false;
                }

                _angleDegrees = value;
                EnterCountStage();
                return true;

            case Stage.SettingCount:
                if (value < 0.5)
                {
                    return false;
                }

                _count = ArrayMath.NormalizeCount(value);
                Commit();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Отмена: незавершённый массив отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _stage = Stage.Idle;
        _subject = null;
        _pivot = Vector3d.Zero;
        _axis = Vector3d.UnitZ;
        _reference = Vector3d.Zero;
        _angleDegrees = 0;
        _count = ArrayMath.DefaultCount;
        _swallowRelease = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool HasAngle => Math.Abs(_angleDegrees) >= ArrayMath.MinStepDegrees;

    private bool OnLeftPress(Vector2d positionPx)
    {
        if (_stage == Stage.SettingCount)
        {
            Commit();
            _swallowRelease = true;
            return true;
        }

        if (_stage != Stage.Idle)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        SceneNode subject = ResolveSubject(hit);
        if (subject.Parent is null)
        {
            return false;
        }

        Vector3d axis = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        if (axis.LengthSquared() <= 0)
        {
            return false;
        }

        _subject = subject;
        _pivot = hit.Point;
        _axis = axis;
        _reference = Vector3d.Zero;
        _angleDegrees = 0;
        _stage = Stage.SettingStep;
        SetVcb(ToolValueKind.Angle, null, true);
        UpdatePreview();
        return true;
    }

    private bool OnLeftRelease()
    {
        if (_swallowRelease)
        {
            _swallowRelease = false;
            return true;
        }

        if (_stage != Stage.SettingStep)
        {
            return false;
        }

        if (HasAngle)
        {
            EnterCountStage();
        }

        return true;
    }

    private void EnterCountStage()
    {
        _stage = Stage.SettingCount;
        SetVcb(ToolValueKind.Count, _count, true);
        UpdatePreview();
    }

    private void UpdateAngle(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return;
        }

        Ray3d ray = viewport.PickRay(positionPx);
        if (!EditingMath.TryIntersectPlane(ray, _pivot, _axis, out Vector3d hitPoint))
        {
            return;
        }

        Vector3d direction = EditingMath.ProjectOnPlane(hitPoint - _pivot, _axis);
        if (direction.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            return;
        }

        if (_reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            // первое движение задаёт опорное направление (как у Rotate)
            _reference = direction;
            return;
        }

        _angleDegrees = EditingMath.SignedAngleDegrees(_reference, direction, _axis);
        SetVcb(ToolValueKind.Angle, _angleDegrees, true);
        UpdatePreview();
    }

    private void Commit()
    {
        if (_stage == Stage.Idle || _subject is not { } subject || !HasAngle || _count < 1)
        {
            return;
        }

        IReadOnlyList<SceneNode> copies = ArrayMath.BuildCopies(
            subject,
            ArrayMath.PolarSteps(_axis, _pivot, _angleDegrees, _count));
        var commands = new List<IEditCommand>(copies.Count);
        foreach (SceneNode copy in copies)
        {
            commands.Add(new AddNodeCommand(subject.Parent!, copy));
        }

        Context.Execute(new CompositeEditCommand(
            string.Format(CultureInfo.InvariantCulture, "Массив (круговой), {0} копий", _count),
            commands));
        Cancel();
    }

    private void UpdatePreview()
    {
        if (_stage == Stage.Idle || _subject is not { } subject)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        BoundingBox3d bounds = M6Transforms.WorldBounds(subject);
        AppendBounds(lines, bounds, ToolPreviewStyle.Default, Transform3d.Identity);
        if (HasAngle)
        {
            IReadOnlyList<Transform3d> steps = ArrayMath.PolarSteps(_axis, _pivot, _angleDegrees, _count);
            foreach (Transform3d step in steps)
            {
                AppendBounds(lines, bounds, ToolPreviewStyle.Guide, step);
            }
        }

        points.Add(new ToolPreviewPoint(_pivot, ToolPreviewStyle.Highlight));
        lines.Add(new ToolPreviewLine(_pivot - (_axis * 20), _pivot + (_axis * 20), ToolPreviewStyle.Guide));
        if (HasAngle)
        {
            labels.Add(new ToolPreviewLabel(
                _pivot + (_axis * 20),
                string.Format(CultureInfo.InvariantCulture, "{0} × {1}", _count, FormatDeg(_angleDegrees))));
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
