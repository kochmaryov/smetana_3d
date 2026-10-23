using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.M6;

/// <summary>
/// Follow Me (задача 5.2): выдавливание плоского профиля по траектории из рёбер.
/// <para>
/// Сценарий: первый клик — грань-профиль; последующие клики — рёбра траектории
/// (цепочка рёбер сцены в пиксельном допуске; каждое следующее ребро продолжает
/// цепочку, ребро, возвращающееся к началу, замыкает путь). Enter/правый клик —
/// построить тело (замкнутый путь — без торцов, незамкнутый — с торцами),
/// Delete убирает последнее ребро, Escape отменяет без изменений.
/// </para>
/// <para>
/// Профиль позиционируется в начале траектории и проводится по ней
/// безвинтовыми кадрами (<see cref="SweepBuilder"/>). Результат — новый
/// меш-узел сцены (одна команда истории); исходная геометрия не изменяется.
/// </para>
/// </summary>
public sealed class FollowMeTool : M6ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Follow Me»).</summary>
    public const string ToolId = "tool.followme";

    /// <summary>Имя создаваемого узла сцены.</summary>
    public const string NodeName = "Follow Me";

    private readonly List<Vector3d> _profile = [];
    private Vector3d _profileNormal;
    private Vector3d _profileCenter;
    private readonly List<Vector3d> _path = [];
    private bool _pathClosed;
    private bool _hasProfile;

    public override string Id => ToolId;

    public override bool IsInteracting => _hasProfile;

    /// <summary>Профиль (мировые кольцевые точки) или пусто.</summary>
    public IReadOnlyList<Vector3d> Profile => _profile;

    /// <summary>Точки траектории (для замкнутой — первая точка повторяется в конце).</summary>
    public IReadOnlyList<Vector3d> Path => _path;

    /// <summary>Траектория замкнута.</summary>
    public bool IsPathClosed => _pathClosed;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                return _hasProfile;

            case PointerEventKind.Press when input.Button == PointerButton.Left && !IsPan(input):
                return OnLeftPress(input.PositionPx);

            case PointerEventKind.Press when input.Button == PointerButton.Right:
                return OnRightPress();

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
                if (_hasProfile && _path.Count >= 2)
                {
                    Commit();
                }
                else if (_hasProfile)
                {
                    Cancel();
                }
                else
                {
                    return false;
                }

                return true;

            case ToolKey.Delete:
                return RemoveLastEdge();

            default:
                return false;
        }
    }

    /// <summary>Траектория строится кликами — числовой ввод не применяется.</summary>
    public override bool TryApplyValue(double value) => false;

    /// <summary>Отмена: незавершённое выдавливание отбрасывается, сцена не меняется.</summary>
    public override void Cancel()
    {
        _profile.Clear();
        _path.Clear();
        _pathClosed = false;
        _hasProfile = false;
        _profileNormal = Vector3d.Zero;
        _profileCenter = Vector3d.Zero;
        SetPreview(ToolPreview.Empty);
    }

    private bool OnLeftPress(Vector2d positionPx)
    {
        if (!_hasProfile)
        {
            return TryBeginProfile(positionPx);
        }

        return TryAppendEdge(positionPx);
    }

    private bool OnRightPress()
    {
        if (!_hasProfile)
        {
            return false;
        }

        if (_path.Count >= 2)
        {
            Commit();
        }
        else
        {
            Cancel();
        }

        return true;
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
        _profileCenter = center * (1.0 / ring.Count);
        _hasProfile = true;
        UpdatePreview();
        return true;
    }

    private bool TryAppendEdge(Vector2d positionPx)
    {
        if (Context.Viewport is not { } viewport
            || !M6PathPicker.TryPickEdge(Context.Scene, viewport, positionPx, out M6EdgeHit hit))
        {
            return true;
        }

        if (_path.Count == 0)
        {
            if ((hit.From - hit.To).Length() <= M6PathPicker.ChainToleranceMm)
            {
                return true;
            }

            // первое ребро: начало — конец, ближайший к профилю
            bool fromFirst = (hit.From - _profileCenter).LengthSquared() <= (hit.To - _profileCenter).LengthSquared();
            _path.Add(fromFirst ? hit.From : hit.To);
            _path.Add(fromFirst ? hit.To : hit.From);
            UpdatePreview();
            return true;
        }

        if (_pathClosed)
        {
            return true;
        }

        Vector3d start = _path[0];
        Vector3d end = _path[^1];
        if (_path.Count >= 3 && M6PathPicker.SamePoint(hit.From, end) && M6PathPicker.SamePoint(hit.To, start))
        {
            _path.Add(hit.To);
            _pathClosed = true;
        }
        else if (_path.Count >= 3 && M6PathPicker.SamePoint(hit.To, end) && M6PathPicker.SamePoint(hit.From, start))
        {
            _path.Add(hit.From);
            _pathClosed = true;
        }
        else if (M6PathPicker.SamePoint(hit.From, end))
        {
            AppendIfLonger(hit.To);
        }
        else if (M6PathPicker.SamePoint(hit.To, end))
        {
            AppendIfLonger(hit.From);
        }
        else if (M6PathPicker.SamePoint(hit.From, start))
        {
            PrependIfLonger(hit.To);
        }
        else if (M6PathPicker.SamePoint(hit.To, start))
        {
            PrependIfLonger(hit.From);
        }

        UpdatePreview();
        return true;
    }

    private void AppendIfLonger(Vector3d point)
    {
        if (!M6PathPicker.SamePoint(point, _path[^1]))
        {
            _path.Add(point);
        }
    }

    private void PrependIfLonger(Vector3d point)
    {
        if (!M6PathPicker.SamePoint(point, _path[0]))
        {
            _path.Insert(0, point);
        }
    }

    private bool RemoveLastEdge()
    {
        if (!_hasProfile || _path.Count == 0)
        {
            return false;
        }

        if (_pathClosed)
        {
            _pathClosed = false;
            _path.RemoveAt(_path.Count - 1);
        }
        else if (_path.Count > 1)
        {
            _path.RemoveAt(_path.Count - 1);
        }

        if (_path.Count <= 1)
        {
            _path.Clear();
        }

        UpdatePreview();
        return true;
    }

    private void Commit()
    {
        if (!_hasProfile || _path.Count < 2)
        {
            return;
        }

        HalfEdgeMesh mesh = SweepBuilder.Build(_profile, _profileNormal, _path, _pathClosed);
        var node = new MeshNode { Mesh = mesh, Name = NodeName };
        Context.Execute(new CompositeEditCommand(
            "Follow Me",
            [new AddNodeCommand(Context.Scene.Root, node)]));
        Cancel();
    }

    private void UpdatePreview()
    {
        if (!_hasProfile)
        {
            SetPreview(ToolPreview.Empty);
            return;
        }

        var lines = new List<ToolPreviewLine>();
        var points = new List<ToolPreviewPoint>();
        var labels = new List<ToolPreviewLabel>();

        AppendLines(lines, _profile, ToolPreviewStyle.Default, close: true);
        foreach (Vector3d point in _profile)
        {
            points.Add(new ToolPreviewPoint(point, ToolPreviewStyle.Default));
        }

        AppendLines(lines, _path, ToolPreviewStyle.Highlight, _pathClosed);
        foreach (Vector3d point in _path)
        {
            points.Add(new ToolPreviewPoint(point, ToolPreviewStyle.Highlight));
        }

        labels.Add(new ToolPreviewLabel(
            _profileCenter,
            string.Format(CultureInfo.InvariantCulture, "Follow Me: {0} рёбер", Math.Max(0, _path.Count - 1))));
        SetPreview(new ToolPreview(lines, points, labels));
    }

    private static void AppendLines(
        List<ToolPreviewLine> lines,
        IReadOnlyList<Vector3d> points,
        ToolPreviewStyle style,
        bool close)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            lines.Add(new ToolPreviewLine(points[i], points[i + 1], style));
        }

        if (close && points.Count > 2)
        {
            lines.Add(new ToolPreviewLine(points[^1], points[0], style));
        }
    }
}
