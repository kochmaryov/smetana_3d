using SmetaNA.Core.Geometry;
using SmetaNA.Manifold.Interop;

namespace SmetaNA.Manifold;

/// <summary>
/// Управляемая обёртка над ManifoldManifold* (C API, MANIFOLD_CBIND, Manifold 3.5.4).
/// Владение: каждый экземпляр владеет своим нативным хранилищем (manifold_alloc_manifold /
/// manifold_delete_manifold, placement-new пары из спика 0.4); промежуточные
/// MeshGL/MeshGL64 создаются и уничтожаются внутри вызовов.
/// </summary>
/// <remarks>
/// <para>
/// Отложенная материализация: <see cref="Boolean"/> только строит CSG-дерево (≈ 0 мс),
/// реальная работа выполняется при первой eager-операции (<see cref="Materialize"/>,
/// <see cref="Volume"/>, <see cref="ToHalfEdgeMesh"/> и т.п.) и кешируется нативно.
/// Свойство <see cref="IsMaterialized"/> позволяет UI-слою материализовать результат
/// осознанно — с прогрессом и отменой на границах стадий.
/// </para>
/// <para>
/// Отмена: <see cref="CancellationToken"/> проверяется между стадиями конвейера;
/// сама нативная материализация — единый вызов без прерывания (отмена внутри нативной
/// фазы требует ExecutionContext C API — см. README).
/// </para>
/// </remarks>
public sealed class ManifoldSolid : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;
    private bool _materialized;

    internal ManifoldSolid(IntPtr handle, bool materialized)
    {
        _handle = handle;
        _materialized = materialized;
    }

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>
    /// Вычислено ли CSG-дерево. false — у отложенного результата булевой операции
    /// (или его трансформации) до первой eager-операции.
    /// </summary>
    public bool IsMaterialized => _materialized;

    /// <summary>Статус валидности из C API: NoError — манифолд замкнут и корректен.</summary>
    public ManifoldError Status
    {
        get
        {
            EnsureMaterialized();
            return ManifoldNative.manifold_status(Handle);
        }
    }

    public bool IsEmpty
    {
        get
        {
            EnsureMaterialized();
            return ManifoldNative.manifold_is_empty(Handle) != 0;
        }
    }

    public bool IsValid => Status == ManifoldError.NoError;

    /// <summary>Род поверхности (тор — 1, сфера — 0); работает только для валидного манифолда.</summary>
    public int Genus
    {
        get
        {
            EnsureMaterialized();
            return ManifoldNative.manifold_genus(Handle);
        }
    }

    public double Volume
    {
        get
        {
            EnsureMaterialized();
            return ManifoldNative.manifold_volume(Handle);
        }
    }

    public double SurfaceArea
    {
        get
        {
            EnsureMaterialized();
            return ManifoldNative.manifold_surface_area(Handle);
        }
    }

    public long VertexCount
    {
        get
        {
            EnsureMaterialized();
            return (long)ManifoldNative.manifold_num_vert(Handle);
        }
    }

    public long EdgeCount
    {
        get
        {
            EnsureMaterialized();
            return (long)ManifoldNative.manifold_num_edge(Handle);
        }
    }

    public long TriangleCount
    {
        get
        {
            EnsureMaterialized();
            return (long)ManifoldNative.manifold_num_tri(Handle);
        }
    }

    // --- конструирование ---

    /// <summary>Меш (треугольный суп, double-точность MeshGL64) → Manifold.</summary>
    public static ManifoldSolid FromMesh(MeshData mesh) => ManifoldBridge.ToSolid(mesh);

    /// <summary>Полумногоугольниковый меш → Manifold (через треугольный суп).</summary>
    public static ManifoldSolid FromHalfEdgeMesh(HalfEdgeMesh mesh) => ManifoldBridge.ToSolid(mesh);

    /// <summary>Нативный примитив Manifold::Cube.</summary>
    public static ManifoldSolid Cube(double sizeX, double sizeY, double sizeZ, bool centered = true)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new ManifoldSolid(
            ManifoldNative.manifold_cube(mem, sizeX, sizeY, sizeZ, centered ? 1 : 0),
            materialized: true);
    }

    /// <summary>Нативный примитив Manifold::Sphere; circularSegments = 0 — значение по умолчанию.</summary>
    public static ManifoldSolid Sphere(double radius, int circularSegments = 0)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new ManifoldSolid(
            ManifoldNative.manifold_sphere(mem, radius, circularSegments),
            materialized: true);
    }

    /// <summary>
    /// Построение через SDF-колбэк (manifold_level_set_seq): колбэк из C# в нативный код.
    /// Используется последовательная версия — безопасна для managed-колбэков.
    /// Конвенция LevelSet: положительное значение SDF — внутри тела.
    /// </summary>
    public static ManifoldSolid FromSdf(
        Func<double, double, double, double> sdf,
        (double X, double Y, double Z) min,
        (double X, double Y, double Z) max,
        double edgeLength,
        double level = 0.0,
        double tolerance = -1.0)
    {
        ArgumentNullException.ThrowIfNull(sdf);
        IntPtr boxMem = ManifoldNative.manifold_alloc_box();
        IntPtr box = ManifoldNative.manifold_box(boxMem, min.X, min.Y, min.Z, max.X, max.Y, max.Z);
        var fn = new ManifoldNative.SdfFn((x, y, z, _) => sdf(x, y, z));
        try
        {
            IntPtr solidMem = ManifoldNative.manifold_alloc_manifold();
            IntPtr solid = ManifoldNative.manifold_level_set_seq(
                solidMem, fn, box, edgeLength, level, tolerance, IntPtr.Zero);
            return new ManifoldSolid(solid, materialized: true);
        }
        finally
        {
            // Делегат обязан жить весь нативный вызов — иначе GC может убрать thunk.
            GC.KeepAlive(fn);
            ManifoldNative.manifold_delete_box(box);
        }
    }

    // --- операции ---

    /// <summary>
    /// Булева операция: строит только CSG-дерево (отложенный результат).
    /// Вычисление — при первой eager-операции или явном <see cref="Materialize"/>.
    /// </summary>
    public ManifoldSolid Boolean(ManifoldSolid other, BooleanOp op)
    {
        ArgumentNullException.ThrowIfNull(other);
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new ManifoldSolid(
            ManifoldNative.manifold_boolean(mem, Handle, other.Handle, (ManifoldOpType)op),
            materialized: false);
    }

    public ManifoldSolid Union(ManifoldSolid other) => Boolean(other, BooleanOp.Union);

    public ManifoldSolid Difference(ManifoldSolid other) => Boolean(other, BooleanOp.Difference);

    public ManifoldSolid Intersection(ManifoldSolid other) => Boolean(other, BooleanOp.Intersection);

    /// <summary>Сдвиг. Наследует отложенность исходного тела.</summary>
    public ManifoldSolid Translate(double x, double y, double z)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new ManifoldSolid(
            ManifoldNative.manifold_translate(mem, Handle, x, y, z),
            materialized: _materialized);
    }

    // --- явная материализация и чтение результата ---

    /// <summary>
    /// Явная материализация CSG-дерева (главная дорогая фаза булевой операции).
    /// Повторные вызовы ничего не делают — результат кешируется нативно.
    /// </summary>
    /// <param name="progress">Прогресс: стадия <see cref="CsgStage.Materialize"/>.</param>
    /// <param name="cancellationToken">Проверяется до и после нативного вызова; сам нативный вызов не прерывается.</param>
    public ManifoldSolid Materialize(IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureMaterialized(progress, cancellationToken);
        return this;
    }

    /// <summary>Результат → треугольный суп (позиции double, индексы; readback MeshGL64).</summary>
    public MeshData ToMeshData(IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => ManifoldBridge.ToMeshData(this, progress, cancellationToken);

    /// <summary>Результат → полумногоугольниковый меш; пустое тело даёт пустой меш.</summary>
    public HalfEdgeMesh ToHalfEdgeMesh(IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
        => ManifoldBridge.ToHalfEdgeMesh(this, progress, cancellationToken);

    internal void EnsureMaterialized(IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_materialized)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(CsgProgress.For(CsgStage.Materialize));

        // Первая eager-операция вычисляет CSG-дерево; нативный результат кешируется.
        _ = ManifoldNative.manifold_num_tri(Handle);

        cancellationToken.ThrowIfCancellationRequested();
        _materialized = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ManifoldNative.manifold_delete_manifold(_handle);
        _handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    ~ManifoldSolid()
    {
        if (!_disposed && _handle != IntPtr.Zero)
        {
            ManifoldNative.manifold_delete_manifold(_handle);
        }
    }
}
