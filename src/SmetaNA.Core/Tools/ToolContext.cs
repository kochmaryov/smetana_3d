using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools;

/// <summary>
/// Контекст инструмента: сцена, выборка, запросы к InferenceEngine,
/// привязка VCB и стек истории (<see cref="UndoStack"/>). UI обновляет
/// <see cref="Viewport"/> перед маршрутизацией ввода.
/// </summary>
public sealed class ToolContext
{
    private InferenceScene? _inferenceScene;
    private InferenceScene? _inferenceSceneWithHidden;

    public ToolContext(Scene scene, UndoStack undoStack)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(undoStack);
        Scene = scene;
        UndoStack = undoStack;
        UndoStack.Changed += InvalidateInferenceCache;
    }

    public Scene Scene { get; }

    public UndoStack UndoStack { get; }

    /// <summary>Выборка инструмента (объект/грань).</summary>
    public ToolSelection Selection { get; } = new();

    /// <summary>Камера и вьюпорт для лучей/проекций; обновляется UI перед вводом.</summary>
    public InferenceViewport? Viewport { get; set; }

    /// <summary>Решатель привязок; <c>null</c> — <see cref="InferenceEngine.Solve"/>.</summary>
    public Func<SnapRequest, SnapResult>? SnapSolver { get; set; }

    private InferenceSettings _inference = InferenceSettings.Default;

    /// <summary>
    /// Настройки привязок (допуск, сетка, категории, подписи, скрытая геометрия).
    /// Значения нормализуются при присваивании.
    /// </summary>
    public InferenceSettings Inference
    {
        get => _inference;
        set
        {
            InferenceSettings normalized = (value ?? InferenceSettings.Default).Normalize();
            if (_inference == normalized)
            {
                return;
            }

            _inference = normalized;
            InferenceChanged?.Invoke();
        }
    }

    /// <summary>Настройки привязок изменились (UI обновляет оверлей и снимок сцены).</summary>
    public event Action? InferenceChanged;

    /// <summary>
    /// Временный показ скрытой геометрии — удержание Ctrl+Shift (R-10). Выставляет UI
    /// по состоянию клавиш; действует, пока модификатор нажат.
    /// </summary>
    public bool TemporaryIncludeHiddenGeometry { get; set; }

    /// <summary>Скрытая геометрия участвует в привязках: постоянный режим или Ctrl+Shift.</summary>
    public bool IncludeHiddenGeometry => Inference.ShowHiddenGeometry || TemporaryIncludeHiddenGeometry;

    /// <summary>
    /// Снимок сцены для привязок с учётом показа скрытой геометрии. Все инструменты
    /// берут снимок отсюда, чтобы режим скрытого действовал одинаково.
    /// </summary>
    public InferenceScene SnapScene => BuildInferenceScene(IncludeHiddenGeometry);

    /// <summary>Сетка привязки в плоскости построения; <c>null</c> — сетка выключена.</summary>
    public SnapGrid? GridFor(DrawingPlane plane)
    {
        if (!Inference.GridEnabled)
        {
            return null;
        }

        return new SnapGrid(plane.Origin, plane.U, plane.V, Inference.GridStep)
        {
            TolerancePx = InferenceSettings.DefaultGridTolerancePx,
        };
    }

    /// <summary>Сетка привязки на плоскости XY через точку (перемещения и размеры).</summary>
    public SnapGrid? GroundGridAt(Vector3d point)
    {
        if (!Inference.GridEnabled)
        {
            return null;
        }

        return new SnapGrid(
            new Vector3d(0, 0, point.Z),
            Vector3d.UnitX,
            Vector3d.UnitY,
            Inference.GridStep)
        {
            TolerancePx = InferenceSettings.DefaultGridTolerancePx,
        };
    }

    /// <summary>Активная фиксация направления стрелками (переключатель).</summary>
    public SnapModifiers LockModifiers { get; private set; }

    /// <summary>Фиксация или режим привязок изменились (индикатор статус-строки, R-24).</summary>
    public event Action? SnapStateChanged;

    /// <summary>Режим линейных привязок, переключаемый Alt.</summary>
    public SnapInferenceMode InferenceMode { get; private set; } = SnapInferenceMode.All;

    private const SnapModifiers LockMask =
        SnapModifiers.LockAxisX | SnapModifiers.LockAxisY | SnapModifiers.LockAxisZ
        | SnapModifiers.LockParallel | SnapModifiers.LockPerpendicular;

    /// <summary>Переключить фиксацию: та же — снять, другая — заменить.</summary>
    public void ToggleSnapLock(SnapModifiers lockModifiers)
    {
        LockModifiers = (LockModifiers & lockModifiers) == lockModifiers
            ? SnapModifiers.None
            : lockModifiers & LockMask;
        SnapStateChanged?.Invoke();
    }

    public void ResetSnapLocks()
    {
        if (LockModifiers == SnapModifiers.None)
        {
            return;
        }

        LockModifiers = SnapModifiers.None;
        SnapStateChanged?.Invoke();
    }

    /// <summary>Цикл Alt: все → только точки → только параллель/перпендикуляр → все.</summary>
    public void CycleInferenceMode()
    {
        InferenceMode = InferenceMode switch
        {
            SnapInferenceMode.All => SnapInferenceMode.PointsOnly,
            SnapInferenceMode.PointsOnly => SnapInferenceMode.ParallelPerpendicularOnly,
            _ => SnapInferenceMode.All,
        };
        SnapStateChanged?.Invoke();
    }

    /// <summary>
    /// Модификаторы привязок с учётом фиксации и режима Alt. Ctrl без Shift — только
    /// точечные привязки; Ctrl+Shift — показ скрытой геометрии (на категории не влияет).
    /// </summary>
    public SnapModifiers ResolveSnapModifiers(ToolModifiers modifiers)
    {
        bool ctrl = (modifiers & ToolModifiers.Ctrl) != 0;
        bool shift = (modifiers & ToolModifiers.Shift) != 0;
        return ResolveSnapModifiers(ctrl && !shift);
    }

    /// <summary>Модификаторы привязок с учётом фиксации и режима Alt; <paramref name="ctrl"/> — только точки.</summary>
    public SnapModifiers ResolveSnapModifiers(bool ctrl = false)
    {
        SnapModifiers modifiers = LockModifiers;
        modifiers |= InferenceMode switch
        {
            SnapInferenceMode.PointsOnly => SnapModifiers.PointsOnly,
            SnapInferenceMode.ParallelPerpendicularOnly => SnapModifiers.ParallelPerpendicularOnly,
            _ => SnapModifiers.None,
        };

        if (ctrl)
        {
            modifiers |= SnapModifiers.PointsOnly;
        }

        return modifiers;
    }

    /// <summary>Состояние числового ввода (VCB) активного инструмента.</summary>
    public ToolVcbState VcbState { get; private set; } = ToolVcbState.Inactive;

    /// <summary>
    /// Пространство координат ввода (R-16, S14). В локальном абсолютная строка
    /// VCB трактуется в осях <see cref="CoordinateContext"/>.
    /// </summary>
    public CoordinateSpace CoordinateSpace { get; private set; } = CoordinateSpace.World;

    /// <summary>
    /// Мировая матрица контекста (выбранного контейнера): начало и оси для
    /// локального ввода. <c>null</c> — контекста нет, локальный режим недоступен.
    /// </summary>
    public Transform3d? CoordinateContext { get; private set; }

    /// <summary>Пространство координат или контекст изменились.</summary>
    public event Action? CoordinateSpaceChanged;

    /// <summary>Есть ли контекст, в осях которого возможен локальный ввод.</summary>
    public bool HasCoordinateContext => CoordinateContext is not null;

    /// <summary>Переключить мировые/локальные координаты (без контекста — остаётся мировым).</summary>
    public void ToggleCoordinateSpace()
    {
        CoordinateSpace = CoordinateSpace == CoordinateSpace.World && HasCoordinateContext
            ? CoordinateSpace.Local
            : CoordinateSpace.World;
        CoordinateSpaceChanged?.Invoke();
    }

    /// <summary>Задать контекст локальных координат (мировая матрица контейнера).</summary>
    public void SetCoordinateContext(Transform3d? context)
    {
        if (CoordinateContext == context)
        {
            return;
        }

        CoordinateContext = context;
        if (context is null && CoordinateSpace == CoordinateSpace.Local)
        {
            CoordinateSpace = CoordinateSpace.World;
        }

        CoordinateSpaceChanged?.Invoke();
    }

    /// <summary>Точка ввода → мир: в локальном режиме — через матрицу контекста.</summary>
    public Vector3d ToWorldPoint(Vector3d point)
        => CoordinateSpace == CoordinateSpace.Local && CoordinateContext is { } context
            ? context.TransformPoint(point)
            : point;

    /// <summary>Вектор/приращение ввода → мир: в локальном режиме — только поворот/масштаб.</summary>
    public Vector3d ToWorldVector(Vector3d vector)
        => CoordinateSpace == CoordinateSpace.Local && CoordinateContext is { } context
            ? context.TransformVector(vector)
            : vector;

    /// <summary>Сцена была изменена командой (UI перестраивает рендер-модель).</summary>
    public event Action? SceneEdited;

    /// <summary>Состояние VCB изменилось.</summary>
    public event Action? VcbStateChanged;

    /// <summary>Запрос привязок к InferenceEngine (или подставленному решателю).</summary>
    public SnapResult Snap(SnapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SnapResult result = (SnapSolver ?? InferenceEngine.Solve)(request);
        return SnapCandidateOffset == 0 ? result : result.WithBest(SnapCandidateOffset);
    }

    /// <summary>
    /// Выбранный Tab кандидат (0 — лучший по приоритету). Перебор сбрасывается
    /// движением указателя.
    /// </summary>
    public int SnapCandidateOffset { get; private set; }

    /// <summary>Перейти к следующему кандидату (Tab, R-25).</summary>
    public void CycleSnapCandidate() => SnapCandidateOffset++;

    /// <summary>Сбросить перебор кандидатов (указатель сдвинулся).</summary>
    public void ResetSnapCandidateOffset() => SnapCandidateOffset = 0;

    /// <summary>
    /// Снимок сцены для запросов привязок. Кэшируется: построение снимка — O(числа
    /// рёбер/граней) с аллокациями, а инструменты запрашивают его на каждое событие
    /// указателя. Кэш сбрасывается на любую правку через <see cref="UndoStack.Changed"/>.
    /// </summary>
    public InferenceScene BuildInferenceScene(bool includeHidden = false)
    {
        if (includeHidden)
        {
            return _inferenceSceneWithHidden ??= InferenceSceneBuilder.FromScene(Scene, includeHidden: true);
        }

        return _inferenceScene ??= InferenceSceneBuilder.FromScene(Scene);
    }

    /// <summary>Сбросить кэш снимка сцены (для внешних правок мимо команд).</summary>
    public void InvalidateInferenceCache()
    {
        _inferenceScene = null;
        _inferenceSceneWithHidden = null;
    }

    /// <summary>
    /// Запрос привязок с текущими настройками: снимок сцены (включая скрытую
    /// геометрию по режиму или Ctrl+Shift), толеранс, категории, сетка и фиксация.
    /// <c>null</c> — нет вьюпорта (инструмент без UI).
    /// </summary>
    /// <param name="cursorPx">Курсор, пиксели вьюпорта.</param>
    /// <param name="anchor">Опорная точка построения.</param>
    /// <param name="modifiers">Модификаторы события указателя.</param>
    /// <param name="locked">Зафиксированный Shift кандидат.</param>
    /// <param name="grid">Сетка; <c>null</c> — без сетки.</param>
    /// <param name="scene">Снимок; <c>null</c> — <see cref="SnapScene"/>.</param>
    public SnapRequest? BuildSnapRequest(
        Vector2d cursorPx,
        Vector3d? anchor,
        ToolModifiers modifiers,
        SnapCandidate? locked = null,
        SnapGrid? grid = null,
        InferenceScene? scene = null,
        SnapCandidate? previousBest = null,
        double previousBestAgeMs = -1)
    {
        if (Viewport is not { } viewport)
        {
            return null;
        }

        SnapRequest request = new()
        {
            Ray = viewport.PickRay(cursorPx),
            Viewport = viewport,
            Scene = scene ?? SnapScene,
            CursorPx = cursorPx,
            TolerancePx = Inference.TolerancePx,
            Filter = Inference.Categories,
            Anchor = anchor,
            Grid = grid,
            Modifiers = ResolveSnapModifiers(modifiers),
            PreviousBest = previousBest,
            PreviousBestAgeMs = previousBestAgeMs,
        };

        return locked is { } candidate ? request with { LockedCandidate = candidate } : request;
    }

    /// <summary>Выбрать грань под курсором (пиксели вьюпорта).</summary>
    public bool TryPickFace(Vector2d cursorPx, out ToolFaceHit hit)
    {
        hit = default;
        if (Viewport is not { } viewport)
        {
            return false;
        }

        return SceneFacePicker.TryPick(Scene, viewport.PickRay(cursorPx), out hit, includeHidden: IncludeHiddenGeometry);
    }

    /// <summary>Выполнить команду редактирования (в стек истории) и оповестить UI.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        InvalidateInferenceCache();
        UndoStack.Push(command);
        SceneEdited?.Invoke();
    }

    /// <summary>Опубликовать состояние числового ввода (вызывается инструментами).</summary>
    public void ReportVcb(ToolValueKind kind, double? value, bool isActive)
    {
        ToolVcbState state = new(isActive, kind, value);
        if (VcbState == state)
        {
            return;
        }

        VcbState = state;
        VcbStateChanged?.Invoke();
    }
}
