using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Tools.Commands;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Eraser (задача 2.3): стирание граней одним жестом — щелчок стирает
/// грань под курсором, перетаскивание собирает все грани под курсором и стирает их
/// одной командой истории (<see cref="EraseFacesCommand"/>). Грани возвращаются при
/// отмене с прежней идентичностью, twin-связи соседних граней восстанавливаются.
/// Отмена (<c>Escape</c>/<see cref="Cancel"/>) прерывает жест без изменения сцены.
/// </summary>
public sealed class EraserTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Eraser»).</summary>
    public const string ToolId = "tool.eraser";

    private enum EraserState
    {
        Idle,
        Erasing,
    }

    private EraserState _state = EraserState.Idle;
    private readonly List<FaceEraseTarget> _pending = [];
    private readonly HashSet<MeshFace> _pendingFaces = [];
    private readonly Dictionary<MeshFace, Transform3d> _pendingWorlds = [];
    private bool _hasHover;
    private MeshFaceRef _hover;
    private Transform3d _hoverWorld = Transform3d.Identity;

    public override string Id => ToolId;

    public override bool IsInteracting => _state == EraserState.Erasing;

    /// <summary>Собираемые для стирания грани (текущий жест).</summary>
    public IReadOnlyList<FaceEraseTarget> Pending => _pending;

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_state == EraserState.Erasing)
                {
                    CollectFace(input.PositionPx);
                    return true;
                }

                UpdateHover(input.PositionPx);
                return false;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры, инструмент не перехватывает
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                return BeginStroke(input.PositionPx);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return EndStroke();

            case PointerEventKind.Leave:
                if (_state == EraserState.Erasing)
                {
                    return true;
                }

                ClearHover();
                return false;

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
            case ToolKey.Enter when _state == EraserState.Erasing:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>У Eraser нет числовых параметров: значение не применяется.</summary>
    public override bool TryApplyValue(double value) => false;

    /// <summary>Отмена текущего жеста: накопленные грани не стираются.</summary>
    public override void Cancel()
    {
        _state = EraserState.Idle;
        _pending.Clear();
        _pendingFaces.Clear();
        _pendingWorlds.Clear();
        BuildPreview();
    }

    private bool BeginStroke(Vector2d positionPx)
    {
        if (_state == EraserState.Erasing)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        _state = EraserState.Erasing;
        _pending.Clear();
        _pendingFaces.Clear();
        _pendingWorlds.Clear();
        Collect(hit);
        return true;
    }

    private bool EndStroke()
    {
        if (_state != EraserState.Erasing)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        _state = EraserState.Idle;
        if (_pending.Count > 0)
        {
            string description = string.Create(
                CultureInfo.InvariantCulture, $"Стирание граней ({_pending.Count})");
            Context.Execute(new EraseFacesCommand(description, [.. _pending]));
        }

        _pending.Clear();
        _pendingFaces.Clear();
        _pendingWorlds.Clear();
        Context.Selection.Clear();
        ClearHover();
    }

    private void CollectFace(Vector2d positionPx)
    {
        if (Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            Collect(hit);
        }
    }

    private void Collect(ToolFaceHit hit)
    {
        if (!_pendingFaces.Add(hit.Face))
        {
            return;
        }

        _pending.Add(new FaceEraseTarget(hit.Node.Mesh, hit.Face));
        _pendingWorlds[hit.Face] = hit.WorldTransform;
        Context.Selection.SetFace(new MeshFaceRef(hit.Node, hit.Face));
        BuildPreview();
    }

    private void UpdateHover(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            ClearHover();
            return;
        }

        _hover = new MeshFaceRef(hit.Node, hit.Face);
        _hoverWorld = hit.WorldTransform;
        _hasHover = true;
        BuildPreview();
    }

    private void ClearHover()
    {
        _hasHover = false;
        _hover = default;
        BuildPreview();
    }

    private void BuildPreview()
    {
        var lines = new List<ToolPreviewLine>();
        foreach (FaceEraseTarget target in _pending)
        {
            Transform3d world = _pendingWorlds.TryGetValue(target.Face, out Transform3d known)
                ? known
                : Transform3d.Identity;
            AppendRingLines(lines, target.Mesh, target.Face, world, ToolPreviewStyle.Highlight);
        }

        if (_hasHover && !_pendingFaces.Contains(_hover.Face))
        {
            AppendRingLines(lines, _hover.Node.Mesh, _hover.Face, _hoverWorld, ToolPreviewStyle.Guide);
        }

        SetPreview(new ToolPreview(lines));
    }

    private void AppendRingLines(
        List<ToolPreviewLine> lines,
        HalfEdgeMesh mesh,
        MeshFace face,
        Transform3d world,
        ToolPreviewStyle style)
    {
        IReadOnlyList<MeshVertex> ring = mesh.GetRing(face);
        for (int i = 0; i < ring.Count; i++)
        {
            lines.Add(new ToolPreviewLine(
                world.TransformPoint(ring[i].Position),
                world.TransformPoint(ring[(i + 1) % ring.Count].Position),
                style));
        }
    }
}
