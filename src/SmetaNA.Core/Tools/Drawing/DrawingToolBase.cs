using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// РћР±С‰Р°СЏ РјРµС…Р°РЅРёРєР° РёРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ СЂРёСЃРѕРІР°РЅРёСЏ (Р·Р°РґР°С‡Р° 2.1): РїР»РѕСЃРєРѕСЃС‚СЊ РїРѕСЃС‚СЂРѕРµРЅРёСЏ
/// (РіСЂР°РЅСЊ РїРѕРґ РєСѓСЂСЃРѕСЂРѕРј РёР»Рё В«Р·РµРјР»СЏВ» XY), СЂР°Р·СЂРµС€РµРЅРёРµ С‚РѕС‡РєРё С‡РµСЂРµР·
/// <see cref="InferenceEngine"/> (РїСЂРёРІСЏР·РєРё) СЃ РѕС‚РєР°С‚РѕРј РЅР° РїРµСЂРµСЃРµС‡РµРЅРёРµ Р»СѓС‡Р° СЃ
/// РїР»РѕСЃРєРѕСЃС‚СЊСЋ, РєРѕРјРјРёС‚ С„РёРіСѓСЂС‹ РєРѕРјР°РЅРґРѕР№ РёСЃС‚РѕСЂРёРё РІ РєРѕРѕСЂРґРёРЅР°С‚Р°С… С†РµР»РµРІРѕРіРѕ РјРµС€Р°.
/// <para>
/// Р¦РµР»СЊ РєРѕРјРјРёС‚Р°: РµСЃР»Рё РїРµСЂРІС‹Р№ РєР»РёРє РїРѕРїР°Р» РІ РіСЂР°РЅСЊ вЂ” РµС‘ РјРµС€ (РіРµРѕРјРµС‚СЂРёСЏ СЂРёСЃСѓРµС‚СЃСЏ
/// РІ РєРѕРѕСЂРґРёРЅР°С‚Р°С… СѓР·Р»Р°); РёРЅР°С‡Рµ СЃРѕР·РґР°С‘С‚СЃСЏ РЅРѕРІС‹Р№ РјРµС€-СѓР·РµР» СЃС†РµРЅС‹. РљР°Р¶РґР°СЏ Р·Р°РІРµСЂС€С‘РЅРЅР°СЏ
/// С„РёРіСѓСЂР° вЂ” РѕРґРЅР° РєРѕРјР°РЅРґР° РІ <c>UndoStack</c> (РѕС‚РјРµРЅР°/РїРѕРІС‚РѕСЂ СЂР°Р±РѕС‚Р°СЋС‚).
/// </para>
/// </summary>
public abstract class DrawingToolBase : ToolBase
{
    /// <summary>РњРёРЅРёРјР°Р»СЊРЅР°СЏ РґР»РёРЅР° СЃРµРіРјРµРЅС‚Р°, РјРј.</summary>
    protected const double MinSegmentLengthMm = 1e-6;

    /// <summary>Р Р°РґРёСѓСЃ В«Р·Р°С…РІР°С‚Р°В» РЅР°С‡Р°Р»СЊРЅРѕР№ С‚РѕС‡РєРё РєРѕРЅС‚СѓСЂР° РґР»СЏ Р·Р°РјС‹РєР°РЅРёСЏ, РїРёРєСЃРµР»РµР№.</summary>
    protected const double CloseTolerancePx = SnapRequest.DefaultTolerancePx;

    private DrawingPlane _plane = DrawingPlane.Ground;
    private bool _surfaceResolved;
    private MeshNode? _targetNode;
    private Transform3d _world = Transform3d.Identity;
    private Transform3d _worldToLocal = Transform3d.Identity;
    private DrawingPlane? _lockedPlane;
    private MeshNode? _lockedNode;
    private Transform3d _lockedWorld = Transform3d.Identity;
    private Transform3d _lockedWorldToLocal = Transform3d.Identity;

    /// <summary>РџР»РѕСЃРєРѕСЃС‚СЊ РїРѕСЃС‚СЂРѕРµРЅРёСЏ (С„РёРєСЃРёСЂСѓРµС‚СЃСЏ РїРµСЂРІС‹Рј РєР»РёРєРѕРј С„РёРіСѓСЂС‹).</summary>
    protected DrawingPlane Plane => _plane;

    /// <summary>РџР»РѕСЃРєРѕСЃС‚СЊ Рё С†РµР»СЊ РІС‹Р±СЂР°РЅС‹ РїРµСЂРІС‹Рј РєР»РёРєРѕРј С„РёРіСѓСЂС‹.</summary>
    protected bool IsSurfaceResolved => _surfaceResolved;

    /// <summary>Р’СЊСЋРїРѕСЂС‚ РєРѕРЅС‚РµРєСЃС‚Р° (РґР»СЏ Р»СѓС‡РµР№ Рё РїРёРєСЃРµР»СЊРЅС‹С… РґРѕРїСѓСЃРєРѕРІ); <c>null</c> Р±РµР· UI.</summary>
    protected InferenceViewport? Viewport => Context.Viewport;

    /// <summary>
    /// РљР»РёРє СЃ Shift вЂ” РїР°РЅРѕСЂР°РјР° РєР°РјРµСЂС‹, РёРЅСЃС‚СЂСѓРјРµРЅС‚ РЅРµ РїРµСЂРµС…РІР°С‚С‹РІР°РµС‚. Shift+Alt вЂ”
    /// С„РёРєСЃР°С†РёСЏ РїР»РѕСЃРєРѕСЃС‚Рё РїРѕСЃС‚СЂРѕРµРЅРёСЏ (R-23), Shift+Ctrl вЂ” СЃРєСЂС‹С‚Р°СЏ РіРµРѕРјРµС‚СЂРёСЏ (R-10):
    /// СЌС‚Рѕ РјРѕРґРёС„РёРєР°С‚РѕСЂС‹ РёРЅСЃС‚СЂСѓРјРµРЅС‚Р°, РїР°РЅРѕСЂР°РјРѕР№ РЅРµ СЃС‡РёС‚Р°СЋС‚СЃСЏ.
    /// </summary>
    protected static bool IsPan(PointerInput input)
        => input.Kind == PointerEventKind.Press
            && (input.Modifiers & ToolModifiers.Shift) != 0
            && (input.Modifiers & (ToolModifiers.Alt | ToolModifiers.Ctrl)) == 0;

    /// <summary>
    /// РћРїСЂРµРґРµР»РёС‚СЊ РїР»РѕСЃРєРѕСЃС‚СЊ Рё С†РµР»СЊ РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕ РїРёРєСЃРµР»СЋ РїРµСЂРІРѕРіРѕ РєР»РёРєР°:
    /// РіСЂР°РЅСЊ РїРѕРґ РєСѓСЂСЃРѕСЂРѕРј (РµС‘ РїР»РѕСЃРєРѕСЃС‚СЊ Рё РјРµС€) Р»РёР±Рѕ В«Р·РµРјР»СЏВ» XY Рё РЅРѕРІС‹Р№ СѓР·РµР».
    /// </summary>
    protected void ResolveSurface(Vector2d cursorPx)
    {
        if (_lockedPlane is { } lockedPlane)
        {
            _targetNode = _lockedNode;
            _world = _lockedWorld;
            _worldToLocal = _lockedWorldToLocal;
            _plane = lockedPlane;
            _surfaceResolved = true;
            return;
        }

        if (TryPlaneFromFace(cursorPx, out ToolFaceHit hit, out DrawingPlane plane, out Transform3d worldToLocal))
        {
            _targetNode = hit.Node;
            _world = hit.WorldTransform;
            _worldToLocal = worldToLocal;
            _plane = plane;
        }
        else
        {
            _targetNode = null;
            _world = Transform3d.Identity;
            _worldToLocal = Transform3d.Identity;
            _plane = DrawingPlane.Ground;
        }

        _surfaceResolved = true;
    }

    /// <summary>
    /// РџР»РѕСЃРєРѕСЃС‚СЊ РіСЂР°РЅРё РїРѕРґ РєСѓСЂСЃРѕСЂРѕРј СЃ С‚СЂР°РЅСЃС„РѕСЂРјР°С†РёРµР№ СѓР·Р»Р°: РґР»СЏ РїРѕСЃС‚СЂРѕРµРЅРёСЏ В«РїРѕ РіСЂР°РЅРёВ»
    /// Рё С„РёРєСЃР°С†РёРё РїР»РѕСЃРєРѕСЃС‚Рё Shift+Alt (R-23).
    /// </summary>
    private bool TryPlaneFromFace(
        Vector2d cursorPx,
        out ToolFaceHit hit,
        out DrawingPlane plane,
        out Transform3d worldToLocal)
    {
        plane = DrawingPlane.Ground;
        worldToLocal = Transform3d.Identity;
        if (!Context.TryPickFace(cursorPx, out hit)
            || !TransformInverter.TryInvert(hit.WorldTransform, out worldToLocal))
        {
            return false;
        }

        Vector3d normal = hit.WorldTransform.TransformVector(MeshNormals.ComputeFaceNormal(hit.Face)).Normalized();
        Vector3d origin = hit.WorldTransform.TransformPoint(MeshEditOps.FaceCentroid(hit.Face));
        IReadOnlyList<MeshVertex> ring = hit.Node.Mesh.GetRing(hit.Face);
        Vector3d? uHint = ring.Count >= 2
            ? hit.WorldTransform.TransformVector(ring[1].Position - ring[0].Position)
            : null;
        plane = DrawingPlane.Create(origin, normal, uHint);
        return true;
    }

    /// <summary>
    /// Р¤РёРєСЃР°С†РёСЏ РїР»РѕСЃРєРѕСЃС‚Рё РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕ РЅР°РІРµРґС‘РЅРЅРѕР№ РіСЂР°РЅРё (Shift+Alt, R-23): РїРѕРєР°
    /// РјРѕРґРёС„РёРєР°С‚РѕСЂ СѓРґРµСЂР¶Р°РЅ, РїРѕСЃС‚СЂРѕРµРЅРёРµ РёРґС‘С‚ РІ РїР»РѕСЃРєРѕСЃС‚Рё РїРѕСЃР»РµРґРЅРµР№ РЅР°РІРµРґС‘РЅРЅРѕР№ РіСЂР°РЅРё,
    /// РґР°Р¶Рµ РµСЃР»Рё РєСѓСЂСЃРѕСЂ СѓС€С‘Р» СЃ РЅРµС‘.
    /// </summary>
    private void UpdatePlaneLock(Vector2d cursorPx, ToolModifiers modifiers)
    {
        const ToolModifiers planeLockModifiers = ToolModifiers.Shift | ToolModifiers.Alt;
        if ((modifiers & planeLockModifiers) != planeLockModifiers)
        {
            _lockedPlane = null;
            _lockedNode = null;
            _lockedWorld = Transform3d.Identity;
            _lockedWorldToLocal = Transform3d.Identity;
            return;
        }

        if (TryPlaneFromFace(cursorPx, out ToolFaceHit hit, out DrawingPlane plane, out Transform3d worldToLocal))
        {
            _lockedPlane = plane;
            _lockedNode = hit.Node;
            _lockedWorld = hit.WorldTransform;
            _lockedWorldToLocal = worldToLocal;
        }
    }

    /// <summary>РЎР±СЂРѕСЃ РїР»РѕСЃРєРѕСЃС‚Рё/С†РµР»Рё (РЅРѕРІР°СЏ С„РёРіСѓСЂР° РѕРїСЂРµРґРµР»СЏРµС‚ РёС… Р·Р°РЅРѕРІРѕ).</summary>
    protected void ResetSurface()
    {
        _surfaceResolved = false;
        _targetNode = null;
        _world = Transform3d.Identity;
        _worldToLocal = Transform3d.Identity;
        _plane = DrawingPlane.Ground;
    }

    /// <summary>
    /// РўРѕС‡РєР° РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕ РєСѓСЂСЃРѕСЂСѓ: Р»СѓС‡С€РёР№ РєР°РЅРґРёРґР°С‚ РїСЂРёРІСЏР·РѕРє
    /// (<see cref="ToolContext.Snap"/>: РєРѕРЅС†С‹, СЃРµСЂРµРґРёРЅС‹, РѕСЃРё, РїРµСЂРїРµРЅРґРёРєСѓР»СЏСЂС‹,
    /// РєР°СЃР°С‚РµР»СЊРЅС‹Рµ вЂ” РїРѕ Р·Р°РїСЂРѕСЃСѓ СЃ СЏРєРѕСЂРµРј) Р»РёР±Рѕ РїРµСЂРµСЃРµС‡РµРЅРёРµ Р»СѓС‡Р° СЃ РїР»РѕСЃРєРѕСЃС‚СЊСЋ.
    /// </summary>
    protected Vector3d ResolvePoint(
        Vector2d cursorPx,
        Vector3d? anchor,
        ToolModifiers modifiers = ToolModifiers.None)
        => ResolvePoint(cursorPx, anchor, modifiers, [], []);

    /// <summary>
    /// РўРѕС‡РєР° РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕ РєСѓСЂСЃРѕСЂСѓ СЃ РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹РјРё СЃСѓС‰РЅРѕСЃС‚СЏРјРё РїСЂРёРІСЏР·РѕРє:
    /// СЃС‚СЂРѕСЏС‰РёРµСЃСЏ РѕРєСЂСѓР¶РЅРѕСЃС‚Рё/РґСѓРіРё (<paramref name="extraCircles"/>) Рё СЂС‘Р±СЂР°
    /// (<paramref name="extraSegments"/>) РІС…РѕРґСЏС‚ РІ СЃРЅРёРјРѕРє СЃС†РµРЅС‹ Р·Р°РїСЂРѕСЃР°.
    /// </summary>
    protected Vector3d ResolvePoint(
        Vector2d cursorPx,
        Vector3d? anchor,
        ToolModifiers modifiers,
        IReadOnlyList<SnapCircle> extraCircles,
        IReadOnlyList<SnapSegment> extraSegments)
    {
        if (Context.Viewport is not { } viewport)
        {
            SetSnap(SnapResult.Empty);
            return _plane.Origin;
        }

        UpdatePlaneLock(cursorPx, modifiers);
        DrawingPlane plane = _lockedPlane ?? _plane;
        Ray3d ray = viewport.PickRay(cursorPx);
        InferenceScene scene = BuildSnapScene(extraCircles, extraSegments);
        bool shiftLock = (modifiers & ToolModifiers.Shift) != 0 && (modifiers & ToolModifiers.Alt) == 0;
        SnapCandidate? locked = shiftLock ? CurrentSnap.Best : null;
        SnapRequest? request = Context.BuildSnapRequest(
            cursorPx,
            anchor,
            modifiers,
            locked,
            grid: Context.GridFor(plane),
            scene: scene,
            previousBest: CurrentSnap.Best,
            previousBestAgeMs: SnapBestAgeMs);
        if (request is null)
        {
            SetSnap(SnapResult.Empty);
            return plane.Origin;
        }

        SnapResult snap = Context.Snap(request);
        SetSnap(snap);
        if (snap.Best is { } best)
        {
            // РџР»РѕСЃРєРѕСЃС‚СЊ Р·Р°С„РёРєСЃРёСЂРѕРІР°РЅР° Shift+Alt вЂ” С‚РѕС‡РєР° РѕСЃС‚Р°С‘С‚СЃСЏ РІ РµС‘ РїР»РѕСЃРєРѕСЃС‚Рё.
            Vector3d snapped = _lockedPlane is null ? best.Point : plane.Project(best.Point);
            return IsExactSnap(best.Type) ? snapped : ApplyLengthSnapping(snapped, anchor);
        }

        Vector3d free = plane.TryIntersect(ray, out Vector3d point) ? point : plane.Origin;
        return ApplyLengthSnapping(free, anchor);
    }

    /// <summary>РўРѕС‡РЅС‹Рµ РіРµРѕРјРµС‚СЂРёС‡РµСЃРєРёРµ РїСЂРёРІСЏР·РєРё РЅРµ РѕРєСЂСѓРіР»СЏСЋС‚СЃСЏ РїСЂРёРІСЏР·РєРѕР№ РґР»РёРЅС‹.</summary>
    protected static bool IsExactSnap(SnapType type) => type is
        SnapType.Origin or SnapType.InstanceOrigin or SnapType.SnapPoint
        or SnapType.Endpoint or SnapType.Midpoint or SnapType.ArcMidpoint
        or SnapType.Center or SnapType.Intersection or SnapType.Grid;

    /// <summary>
    /// РџСЂРёРІСЏР·РєР° РґР»РёРЅС‹ Рє С‚РѕС‡РЅРѕСЃС‚Рё (R-14.1): РґР»РёРЅР° РѕС‚ РѕРїРѕСЂРЅРѕР№ С‚РѕС‡РєРё РѕРєСЂСѓРіР»СЏРµС‚СЃСЏ РґРѕ
    /// С€Р°РіР° С‚РѕС‡РЅРѕСЃС‚Рё, РЅР°РїСЂР°РІР»РµРЅРёРµ СЃРѕС…СЂР°РЅСЏРµС‚СЃСЏ.
    /// </summary>
    protected Vector3d ApplyLengthSnapping(Vector3d point, Vector3d? anchor)
    {
        if (!Context.Inference.LengthSnappingEnabled || anchor is not { } origin)
        {
            return point;
        }

        Vector3d delta = point - origin;
        double length = delta.Length();
        if (length <= MinSegmentLengthMm)
        {
            return point;
        }

        double precision = Context.Inference.LengthPrecision;
        double snapped = Math.Round(length / precision) * precision;
        if (snapped <= 0)
        {
            return point;
        }

        return origin + delta * (snapped / length);
    }

    /// <summary>
    /// Р Р°Р·СЂРµС€РёС‚СЊ Рё РѕРїСѓР±Р»РёРєРѕРІР°С‚СЊ РїСЂРёРІСЏР·РєСѓ РїРѕРґ РєСѓСЂСЃРѕСЂРѕРј РґРѕ РїРµСЂРІРѕРіРѕ РєР»РёРєР°:
    /// РёРЅСЃС‚СЂСѓРјРµРЅС‚ РїРѕРєР°Р·С‹РІР°РµС‚ РјР°СЂРєРµСЂС‹ РµС‰С‘ РґРѕ РЅР°С‡Р°Р»Р° РїРѕСЃС‚СЂРѕРµРЅРёСЏ.
    /// </summary>
    protected void PreviewSnapAt(Vector2d cursorPx, ToolModifiers modifiers = ToolModifiers.None)
        => ResolvePoint(cursorPx, anchor: null, modifiers);

    /// <summary>РџСЂРёРјРµРЅРёС‚СЊ С‚РѕС‡РєСѓ РёР· VCB (Р°Р±СЃРѕР»СЋС‚РЅСѓСЋ РёР»Рё РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅСѓСЋ).</summary>
    public override bool TryApplyPoint(Vector3d point, bool relative)
        => IsActive && TryApplyPointCore(point, relative);

    /// <summary>РЁР°Рі РїРѕСЃС‚СЂРѕРµРЅРёСЏ РїРѕ РєРѕРѕСЂРґРёРЅР°С‚Рµ; <c>false</c> вЂ” РёРЅСЃС‚СЂСѓРјРµРЅС‚ РЅРµ РїРѕРґРґРµСЂР¶РёРІР°РµС‚.</summary>
    protected virtual bool TryApplyPointCore(Vector3d point, bool relative) => false;

    /// <summary>РџСЂРѕРµРєС†РёСЏ С‚РѕС‡РєРё РЅР° РїР»РѕСЃРєРѕСЃС‚СЊ РїРѕСЃС‚СЂРѕРµРЅРёСЏ (С„РёРіСѓСЂС‹ вЂ” РїР»РѕСЃРєРёРµ).</summary>
    protected Vector3d OnPlane(Vector3d point) => _plane.Project(point);

    /// <summary>РљСѓСЂСЃРѕСЂ СЂСЏРґРѕРј СЃ РјРёСЂРѕРІРѕР№ С‚РѕС‡РєРѕР№ (РїРёРєСЃРµР»СЊРЅС‹Р№ РґРѕРїСѓСЃРє вЂ” Р·Р°РјС‹РєР°РЅРёРµ РєРѕРЅС‚СѓСЂР°).</summary>
    protected bool IsNearInPixels(Vector2d cursorPx, Vector3d worldPoint, double tolerancePx = CloseTolerancePx)
        => Context.Viewport is { } viewport
            && viewport.TryProject(worldPoint) is { } projected
            && Vector2d.Distance(projected, cursorPx) <= tolerancePx;

    /// <summary>
    /// РљРѕРјРјРёС‚ С„РёРіСѓСЂС‹ РѕРґРЅРѕР№ РєРѕРјР°РЅРґРѕР№ РёСЃС‚РѕСЂРёРё: РІ РјРµС€ С†РµР»Рё (РїРµСЂРІС‹Р№ РєР»РёРє РїРѕРїР°Р» РІ
    /// РіСЂР°РЅСЊ) Р»РёР±Рѕ РІ РЅРѕРІС‹Р№ РјРµС€-СѓР·РµР» СЃС†РµРЅС‹.
    /// </summary>
    protected void CommitShape(string description, string nodeName, DrawingShapeSpec spec)
    {
        if (_targetNode is { } target)
        {
            Context.Execute(new AddDrawingShapeCommand(target.Mesh, spec.Transform(_worldToLocal), description));
            return;
        }

        var mesh = new HalfEdgeMesh();
        var node = new MeshNode { Mesh = mesh, Name = nodeName };
        Context.Execute(new CompositeEditCommand(
            description,
            [
                new AddNodeCommand(Context.Scene.Root, node),
                new AddDrawingShapeCommand(mesh, spec, description),
            ]));
    }

    /// <summary>РџРѕРґРїРёСЃСЊ РїСЂРµРґРїСЂРѕСЃРјРѕС‚СЂР° Сѓ РјРёСЂРѕРІРѕР№ С‚РѕС‡РєРё.</summary>
    protected static ToolPreviewLabel LabelAt(Vector3d anchor, string text) => new(anchor, text);

    /// <summary>Р Р°Р·РјРµСЂ РІ РјРёР»Р»РёРјРµС‚СЂР°С… РґР»СЏ РїРѕРґРїРёСЃРё/РѕРїРёСЃР°РЅРёСЏ (РёРЅРІР°СЂРёР°РЅС‚РЅС‹Р№ С„РѕСЂРјР°С‚).</summary>
    protected static string FormatMm(double value)
        => string.Format(CultureInfo.InvariantCulture, "{0:0.###} mm", value);

    /// <summary>Р›РёРЅРёРё РїСЂРµРґРїСЂРѕСЃРјРѕС‚СЂР° РїРѕ С‚РѕС‡РєР°Рј (СЃ Р·Р°РјС‹РєР°РЅРёРµРј РєРѕР»СЊС†Р° РїРѕ Р·Р°РїСЂРѕСЃСѓ).</summary>
    protected static void AppendLines(
        List<ToolPreviewLine> lines,
        IReadOnlyList<Vector3d> points,
        ToolPreviewStyle style,
        bool close = false)
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

    private InferenceScene BuildSnapScene(
        IReadOnlyList<SnapCircle> extraCircles,
        IReadOnlyList<SnapSegment> extraSegments)
    {
        InferenceScene scene = Context.SnapScene;
        if (extraCircles.Count == 0 && extraSegments.Count == 0)
        {
            return scene;
        }

        return scene.WithExtra(extraSegments, extraCircles);
    }
}
