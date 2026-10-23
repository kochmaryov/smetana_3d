using System.Globalization;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Materials;
using SmetaNA.Core.Tools.Commands;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Editing;

/// <summary>
/// Инструмент Paint Bucket (задачи 2.3 и 5.4): назначение материала/цвета граням —
/// щелчок/перетаскивание красит грани под курсором текущим материалом, Ctrl —
/// материал объекта (всему мешу, явные назначения граней снимаются), Alt —
/// пипетка (взять материал с грани). Точный ввод VCB — индекс материала в палитре.
/// <para>
/// Назначения идут через историю (<c>SetFaceMaterialsCommand</c>/
/// <c>SetMeshMaterialCommand</c>); индекс материала — в палитре меша
/// (<c>HalfEdgeMesh.Materials</c>), отсутствие индекса наследует материал объекта.
/// </para>
/// </summary>
public sealed class PaintBucketTool : ToolBase
{
    /// <summary>Идентификатор инструмента (команда «Paint Bucket»).</summary>
    public const string ToolId = "tool.paint";

    private enum PaintState
    {
        Idle,
        Painting,
    }

    private PaintState _state = PaintState.Idle;
    private Material _currentMaterial = MaterialPalette.Default;
    private MaterialTable _palette = MaterialPalette.CreateDefault();
    private bool _objectStroke;
    private HalfEdgeMesh? _objectMesh;
    private readonly List<(HalfEdgeMesh Mesh, MeshFace Face)> _pending = [];
    private readonly HashSet<MeshFace> _pendingFaces = [];
    private readonly Dictionary<MeshFace, Transform3d> _pendingWorlds = [];
    private bool _hasHover;
    private MeshFaceRef _hover;
    private Transform3d _hoverWorld = Transform3d.Identity;

    public override string Id => ToolId;

    public override bool IsInteracting => _state == PaintState.Painting;

    /// <summary>Текущий материал (красит щелчки и жесты).</summary>
    public Material CurrentMaterial
    {
        get => _currentMaterial;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _currentMaterial = value;
            SetVcb(ToolValueKind.Coordinate, CurrentPaletteIndex, IsActive);
            RaiseStateChanged();
        }
    }

    /// <summary>Палитра выбора материала (VCB: индекс в этой палитре).</summary>
    public MaterialTable Palette
    {
        get => _palette;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _palette = value;
            SetVcb(ToolValueKind.Coordinate, CurrentPaletteIndex, IsActive);
        }
    }

    /// <summary>Индекс текущего материала в <see cref="Palette"/> или -1.</summary>
    public int CurrentPaletteIndex => _palette.IndexOf(_currentMaterial);

    protected override void OnActivated()
    {
        SetVcb(ToolValueKind.Coordinate, CurrentPaletteIndex, true);
    }

    public override bool OnPointer(PointerInput input)
    {
        if (!IsActive)
        {
            return false;
        }

        switch (input.Kind)
        {
            case PointerEventKind.Move:
                if (_state == PaintState.Painting)
                {
                    CollectFace(input.PositionPx);
                    return true;
                }

                UpdateHover(input.PositionPx);
                return false;

            case PointerEventKind.Press when input.Button == PointerButton.Left:
                // Shift+ЛКМ — панорама камеры; Alt — пипетка
                if ((input.Modifiers & ToolModifiers.Shift) != 0)
                {
                    return false;
                }

                if ((input.Modifiers & ToolModifiers.Alt) != 0)
                {
                    return SampleMaterial(input.PositionPx);
                }

                return BeginStroke(input.PositionPx, (input.Modifiers & ToolModifiers.Ctrl) != 0);

            case PointerEventKind.Release when input.Button == PointerButton.Left:
                return EndStroke();

            case PointerEventKind.Leave:
                if (_state == PaintState.Painting)
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
            case ToolKey.Enter when _state == PaintState.Painting:
                Commit();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Числовой ввод VCB — индекс материала в <see cref="Palette"/>.</summary>
    public override bool TryApplyValue(double value)
    {
        if (!IsActive || !double.IsFinite(value))
        {
            return false;
        }

        int index = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (!_palette.TryGet(index, out Material? material))
        {
            return false;
        }

        CurrentMaterial = material;
        return true;
    }

    /// <summary>Отмена текущего жеста: материалы не назначаются.</summary>
    public override void Cancel()
    {
        _state = PaintState.Idle;
        _objectStroke = false;
        _objectMesh = null;
        _pending.Clear();
        _pendingFaces.Clear();
        _pendingWorlds.Clear();
        SetPreview(ToolPreview.Empty);
    }

    private bool SampleMaterial(Vector2d positionPx)
    {
        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        CurrentMaterial = MaterialResolver.Resolve(hit.Node.Mesh, hit.Face);
        SetVcb(ToolValueKind.Coordinate, CurrentPaletteIndex, true);
        return true;
    }

    private bool BeginStroke(Vector2d positionPx, bool objectMode)
    {
        if (_state == PaintState.Painting)
        {
            return true;
        }

        if (!Context.TryPickFace(positionPx, out ToolFaceHit hit))
        {
            return false;
        }

        _state = PaintState.Painting;
        _objectStroke = objectMode;
        _objectMesh = objectMode ? hit.Node.Mesh : null;
        _pending.Clear();
        _pendingFaces.Clear();
        if (!objectMode)
        {
            Collect(hit);
        }
        else
        {
            Context.Selection.SetFace(new MeshFaceRef(hit.Node, hit.Face));
            BuildPreview();
        }

        return true;
    }

    private bool EndStroke()
    {
        if (_state != PaintState.Painting)
        {
            return false;
        }

        Commit();
        return true;
    }

    private void Commit()
    {
        _state = PaintState.Idle;
        if (_objectStroke && _objectMesh is { } mesh)
        {
            CommitObjectPaint(mesh);
        }
        else if (_pending.Count > 0)
        {
            CommitFacePaint();
        }

        _objectStroke = false;
        _objectMesh = null;
        _pending.Clear();
        _pendingFaces.Clear();
        _pendingWorlds.Clear();
        Context.Selection.Clear();
        ClearHover();
    }

    private void CommitFacePaint()
    {
        var assignments = new List<FaceMaterialAssignment>();
        foreach ((HalfEdgeMesh mesh, MeshFace face) in _pending)
        {
            int index = mesh.Materials.IndexOfOrAdd(_currentMaterial);
            if (face.MaterialIndex == index)
            {
                continue;
            }

            assignments.Add(new FaceMaterialAssignment(face, face.MaterialIndex, index));
        }

        if (assignments.Count == 0)
        {
            return;
        }

        string description = string.Create(CultureInfo.InvariantCulture, $"Материал: {_currentMaterial.Name}");
        Context.Execute(new SetFaceMaterialsCommand(description, assignments));
    }

    private void CommitObjectPaint(HalfEdgeMesh mesh)
    {
        int index = mesh.Materials.IndexOfOrAdd(_currentMaterial);
        var commands = new List<IEditCommand>
        {
            new SetMeshMaterialCommand(mesh, index),
        };

        var assignments = new List<FaceMaterialAssignment>();
        foreach (MeshFace face in mesh.Faces)
        {
            // явные назначения снимаются: объект красится единым материалом
            if (face.MaterialIndex >= 0 && face.MaterialIndex != index)
            {
                assignments.Add(new FaceMaterialAssignment(face, face.MaterialIndex, -1));
            }
        }

        if (assignments.Count > 0)
        {
            commands.Add(new SetFaceMaterialsCommand("Материал объекта", assignments));
        }

        string description = string.Create(CultureInfo.InvariantCulture, $"Материал: {_currentMaterial.Name}");
        Context.Execute(commands.Count == 1
            ? commands[0]
            : new CompositeEditCommand(description, commands));
    }

    private void CollectFace(Vector2d positionPx)
    {
        if (_objectStroke)
        {
            return;
        }

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

        _pending.Add((hit.Node.Mesh, hit.Face));
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
        Context.Selection.SetFace(_hover);
        SetVcb(ToolValueKind.Coordinate, CurrentPaletteIndex, true);
        BuildPreview();
    }

    private void ClearHover()
    {
        _hasHover = false;
        _hover = default;
        SetPreview(ToolPreview.Empty);
    }

    private void BuildPreview()
    {
        var lines = new List<ToolPreviewLine>();
        foreach ((HalfEdgeMesh mesh, MeshFace face) in _pending)
        {
            Transform3d world = _pendingWorlds.TryGetValue(face, out Transform3d known)
                ? known
                : Transform3d.Identity;
            AppendRingLines(lines, mesh, face, world, ToolPreviewStyle.Highlight);
        }

        if (_hasHover && !_pendingFaces.Contains(_hover.Face))
        {
            AppendRingLines(lines, _hover.Node.Mesh, _hover.Face, _hoverWorld, ToolPreviewStyle.Highlight);
        }

        var labels = new List<ToolPreviewLabel>();
        if (_hasHover)
        {
            labels.Add(new ToolPreviewLabel(
                _hoverWorld.TransformPoint(MeshEditOps.FaceCentroid(_hover.Face)),
                _currentMaterial.Name));
        }

        SetPreview(new ToolPreview(lines, [], labels));
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
