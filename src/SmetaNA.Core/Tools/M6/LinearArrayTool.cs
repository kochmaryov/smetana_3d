using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Линейный массив (задача 5.1): копии выбранного объекта через равные
/// промежутки вдоль заданного направления.
/// <para>
/// Сценарий: клик по объекту (или объект выбран заранее) — базовая точка;
/// перетаскивание задаёт шаг (VCB — точное расстояние, мм); отпускание (или
/// значение VCB) фиксирует шаг и переходит к числу копий (VCB — количество,
/// ввод фиксирует массив). Enter/клик фиксируют массив с текущими
/// параметрами, Escape отменяет без изменений.
/// </para>
/// <para>
/// Результат — копии узлов (<see cref="M6NodeCloner.DeepCopy"/>): обычные
/// объекты копируются независимо, инстансы компонентов копируются как инстансы
/// того же определения (правка мастера распространяется на весь массив).
/// Массив — одна составная команда истории (<see cref="CompositeEditCommand"/>):
/// одна отмена убирает все копии.
/// </para>
/// </summary>
public sealed class LinearArrayTool : M6ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Массив (линейный)»).</summary>
    public const string ToolId = "tool.array.linear";

    private enum Stage
    {
        Idle,
        SettingStep,
        SettingCount,
    }

    private Stage _stage = Stage.Idle;
    private SceneNode? _subject;
    private Vector3d _baseWorld;
    private Vector3d _step;
    private int _count = ArrayMath.DefaultCount;
    private bool _swallowRelease;

    public override string Id => ToolId;

    public override bool IsInteracting => _stage == Stage.SettingStep;

    /// <summary>Текущий шаг массива (мм, мир).</summary>
    public Vector3d Step => _step;

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

                UpdateStep(input.PositionPx);
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
                if (_stage == Stage.SettingCount || (_stage == Stage.SettingStep && HasStep))
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
    /// Числовой ввод VCB: в фазе шага — точное расстояние (направление задаёт
    /// перетаскивание), в фазе количества — число копий, фиксирующее массив.
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
                if (!HasStep || value < ArrayMath.MinStepMm)
                {
                    return false;
                }

                _step = _step.Normalized() * value;
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
        _baseWorld = Vector3d.Zero;
        _step = Vector3d.Zero;
        _count = ArrayMath.DefaultCount;
        _swallowRelease = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool HasStep => _step.Length() >= ArrayMath.MinStepMm;

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

        _subject = subject;
        _baseWorld = hit.Point;
        _step = Vector3d.Zero;
        _stage = Stage.SettingStep;
        SetVcb(ToolValueKind.Length, null, true);
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

        if (HasStep)
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

    private void UpdateStep(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return;
        }

        if (!Editing.EditingMath.TryDragPlaneHit(viewport, positionPx, _baseWorld, out Vector3d target))
        {
            return;
        }

        _step = target - _baseWorld;
        SetVcb(ToolValueKind.Length, HasStep ? _step.Length() : null, true);
        UpdatePreview();
    }

    private void Commit()
    {
        if (_stage == Stage.Idle || _subject is not { } subject || !HasStep || _count < 1)
        {
            return;
        }

        IReadOnlyList<SceneNode> copies = ArrayMath.BuildCopies(
            subject,
            ArrayMath.LinearSteps(_step, _count));
        var commands = new List<IEditCommand>(copies.Count);
        foreach (SceneNode copy in copies)
        {
            commands.Add(new AddNodeCommand(subject.Parent!, copy));
        }

        Context.Execute(new CompositeEditCommand(
            string.Format(CultureInfo.InvariantCulture, "Массив (линейный), {0} копий", _count),
            commands));
        Cancel();
    }

    private void UpdatePreview()
    {
        if (_stage == Stage.Idle || _subject is not { } subject || !HasStep)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        BoundingBox3d bounds = M6Transforms.WorldBounds(subject);
        AppendBounds(lines, bounds, ToolPreviewStyle.Default, Transform3d.Identity);
        for (int k = 1; k <= _count; k++)
        {
            AppendBounds(
                lines,
                bounds,
                ToolPreviewStyle.Guide,
                Transform3d.CreateTranslation(_step * k));
        }

        points.Add(new ToolPreviewPoint(_baseWorld, ToolPreviewStyle.Highlight));
        lines.Add(new ToolPreviewLine(_baseWorld, _baseWorld + _step, ToolPreviewStyle.Highlight));
        labels.Add(new ToolPreviewLabel(
            _baseWorld + (_step * (_count + 1) * 0.5),
            string.Format(CultureInfo.InvariantCulture, "{0} × {1}", _count, FormatMm(_step.Length()))));

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
