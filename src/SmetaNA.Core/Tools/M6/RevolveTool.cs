using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Tools.Editing;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Вращение профиля вокруг оси (revolve, задача 5.2): плоский профиль
/// вращается вокруг оси на заданный угол с заданным числом сегментов.
/// <para>
/// Сценарий: клик 1 — грань-профиль; клики 2–3 — две точки оси (в плоскости
/// профиля); перетаскивание задаёт угол (VCB — точный угол,°; первое движение
/// задаёт опорное направление), затем VCB — число сегментов тесселяции, ввод
/// фиксирует тело. Enter/клик фиксируют вращение с текущими параметрами,
/// Escape отменяет без изменений. Полный угол (±360°) строит замкнутое тело
/// без торцов.
/// </para>
/// <para>
/// Результат — новый меш-узел сцены (одна команда истории); исходная
/// геометрия не изменяется.
/// </para>
/// </summary>
public sealed class RevolveTool : M6ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Вращение»).</summary>
    public const string ToolId = "tool.revolve";

    /// <summary>Имя создаваемого узла сцены.</summary>
    public const string NodeName = "Вращение";

    /// <summary>Порог вырожденного опорного направления.</summary>
    public const double ReferenceEpsilon = 1e-6;

    private enum Stage
    {
        Idle,
        PickingAxisFirst,
        PickingAxisSecond,
        SettingAngle,
        SettingSegments,
    }

    private Stage _stage = Stage.Idle;
    private readonly List<Vector3d> _profile = [];
    private Vector3d _profilePlanePoint;
    private Vector3d _profileNormal;
    private Vector3d _profileCenter;
    private Vector3d _axisPoint;
    private Vector3d _axisDirection = Vector3d.UnitZ;
    private Vector3d _reference;
    private double _angleDegrees;
    private int _segments = RevolveBuilder.DefaultSegments;
    private bool _swallowRelease;

    public override string Id => ToolId;

    public override bool IsInteracting => _stage != Stage.Idle;

    /// <summary>Профиль (мировые кольцевые точки) или пусто.</summary>
    public IReadOnlyList<Vector3d> Profile => _profile;

    /// <summary>Ось вращения (точка + направление) после задания двумя точками.</summary>
    public (Vector3d Point, Vector3d Direction) Axis => (_axisPoint, _axisDirection);

    /// <summary>Текущий угол (градусы, знаковый).</summary>
    public double AngleDegrees => _angleDegrees;

    /// <summary>Число сегментов тесселяции.</summary>
    public int Segments => _segments;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_stage != Stage.SettingAngle)
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
                if (_stage == Stage.SettingSegments || (_stage == Stage.SettingAngle && HasAngle))
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
    /// Числовой ввод VCB: в фазе угла — точный угол (°), затем число сегментов
    /// (ввод фиксирует тело вращения).
    /// </summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        switch (_stage)
        {
            case Stage.SettingAngle:
                if (Math.Abs(value) < ArrayMath.MinStepDegrees
                    || _reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
                {
                    return false;
                }

                _angleDegrees = value;
                _segments = RevolveBuilder.DefaultSegmentsFor(value);
                EnterSegmentsStage();
                return true;

            case Stage.SettingSegments:
                if (value < 1.5)
                {
                    return false;
                }

                _segments = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 2, RevolveBuilder.MaxSegments);
                Commit();
                return true;

            default:
                return false;
        }
    }

    /// <summary>Отмена: незавершённое вращение отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _stage = Stage.Idle;
        _profile.Clear();
        _profilePlanePoint = Vector3d.Zero;
        _profileNormal = Vector3d.Zero;
        _profileCenter = Vector3d.Zero;
        _axisPoint = Vector3d.Zero;
        _axisDirection = Vector3d.UnitZ;
        _reference = Vector3d.Zero;
        _angleDegrees = 0;
        _segments = RevolveBuilder.DefaultSegments;
        _swallowRelease = false;
        SetVcb(ToolValueKind.None, null, false);
        SetPreview(ToolPreview.Empty);
    }

    private bool HasAngle => Math.Abs(_angleDegrees) >= ArrayMath.MinStepDegrees;

    private bool OnLeftPress(Vector2d positionPx)
    {
        switch (_stage)
        {
            case Stage.SettingSegments:
                Commit();
                _swallowRelease = true;
                return true;

            case Stage.SettingAngle:
                if (HasAngle)
                {
                    _segments = RevolveBuilder.DefaultSegmentsFor(_angleDegrees);
                    Commit();
                    _swallowRelease = true;
                }

                return true;

            case Stage.Idle:
                return TryBeginProfile(positionPx);

            default:
                return TryPickAxisPoint(positionPx);
        }
    }

    private bool OnLeftRelease()
    {
        if (_swallowRelease)
        {
            _swallowRelease = false;
            return true;
        }

        return _stage == Stage.SettingAngle || _stage == Stage.SettingSegments;
    }

    private bool TryBeginProfile(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        var ring = new List<Vector3d>();
        foreach (MeshVertex vertex in hit.Node.Mesh.GetRing(hit.Face))
        {
            ring.Add(hit.WorldTransform.TransformPoint(vertex.Position));
        }

        if (ring.Count < 3 || !DrawingShapes.IsPlanar(ring))
        {
            return false;
        }

        Vector3d normal = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        Vector3d center = Vector3d.Zero;
        foreach (Vector3d point in ring)
        {
            center += point;
        }

        _profile.Clear();
        _profile.AddRange(ring);
        _profileNormal = normal;
        _profilePlanePoint = hit.Point;
        _profileCenter = center * (1.0 / ring.Count);
        _stage = Stage.PickingAxisFirst;
        UpdatePreview();
        return true;
    }

    private bool TryPickAxisPoint(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport
            || !EditingMath.TryIntersectPlane(
                viewport.PickRay(positionPx),
                _profilePlanePoint,
                _profileNormal,
                out Vector3d point))
        {
            return true;
        }

        if (_stage == Stage.PickingAxisFirst)
        {
            _axisPoint = point;
            _stage = Stage.PickingAxisSecond;
            UpdatePreview();
            return true;
        }

        Vector3d direction = point - _axisPoint;
        if (direction.Length() < ReferenceEpsilon)
        {
            return true;
        }

        _axisDirection = direction.Normalized();
        _reference = EditingMath.ProjectOnPlane(_profileCenter - _axisPoint, _axisDirection);
        _angleDegrees = 0;
        _stage = Stage.SettingAngle;
        SetVcb(ToolValueKind.Angle, null, true);
        UpdatePreview();
        return true;
    }

    private void UpdateAngle(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport)
        {
            return;
        }

        if (!EditingMath.TryIntersectPlane(
            viewport.PickRay(positionPx),
            _axisPoint,
            _axisDirection,
            out Vector3d hitPoint))
        {
            return;
        }

        Vector3d direction = EditingMath.ProjectOnPlane(hitPoint - _axisPoint, _axisDirection);
        if (direction.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            return;
        }

        if (_reference.LengthSquared() <= ReferenceEpsilon * ReferenceEpsilon)
        {
            // профиль на оси: опорное направление захватывается первым движением
            _reference = direction;
            return;
        }

        _angleDegrees = EditingMath.SignedAngleDegrees(_reference, direction, _axisDirection);
        SetVcb(ToolValueKind.Angle, _angleDegrees, true);
        UpdatePreview();
    }

    private void EnterSegmentsStage()
    {
        _stage = Stage.SettingSegments;
        SetVcb(ToolValueKind.Count, _segments, true);
        UpdatePreview();
    }

    private void Commit()
    {
        if (_stage is Stage.Idle or Stage.PickingAxisFirst or Stage.PickingAxisSecond || !HasAngle)
        {
            return;
        }

        bool wraps = Math.Abs(Math.Abs(_angleDegrees) - 360) <= 1e-9;
        if (wraps && _segments < 3)
        {
            _segments = Math.Max(_segments, 3);
        }

        HalfEdgeMesh mesh = RevolveBuilder.Build(
            _profile,
            _axisPoint,
            _axisDirection,
            _angleDegrees,
            _segments);
        var node = new MeshNode { Mesh = mesh, Name = NodeName };
        Context.Execute(new CompositeEditCommand(
            string.Format(CultureInfo.InvariantCulture, "Вращение {0:0.###}°", _angleDegrees),
            [new AddNodeCommand(Context.Scene.Root, node)]));
        Cancel();
    }

    private void UpdatePreview()
    {
        if (_stage == Stage.Idle || _profile.Count == 0)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        for (int i = 0; i < _profile.Count; i++)
        {
            lines.Add(new ToolPreviewLine(_profile[i], _profile[(i + 1) % _profile.Count], ToolPreviewStyle.Default));
        }

        if (_stage == Stage.PickingAxisFirst)
        {
            labels.Add(new ToolPreviewLabel(_profileCenter, "Ось: точка 1"));
        }
        else
        {
            points.Add(new ToolPreviewPoint(_axisPoint, ToolPreviewStyle.Highlight));
            lines.Add(new ToolPreviewLine(
                _axisPoint - (_axisDirection * 50),
                _axisPoint + (_axisDirection * 50),
                ToolPreviewStyle.Guide));
            if (_stage == Stage.PickingAxisSecond)
            {
                labels.Add(new ToolPreviewLabel(_axisPoint, "Ось: точка 2"));
            }
            else if (HasAngle)
            {
                labels.Add(new ToolPreviewLabel(
                    _profileCenter,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}, {1} сегм.",
                        FormatDeg(_angleDegrees),
                        _segments)));
            }
        }

        SetPreview(new ToolPreview(lines, points, labels));
    }
}
