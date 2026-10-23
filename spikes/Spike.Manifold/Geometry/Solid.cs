using System.Runtime.InteropServices;

namespace Spike.Manifold;

/// <summary>
/// Управляемая обёртка над ManifoldManifold* (C API, MANIFOLD_CBIND).
/// Владение: каждый экземпляр владеет своим нативным хранилищем (manifold_alloc_manifold /
/// manifold_delete_manifold); промежуточные MeshGL/MeshGL64 создаются и уничтожаются внутри вызовов.
/// </summary>
public sealed class Solid : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    internal Solid(IntPtr handle) => _handle = handle;

    private IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>Статус валидности из C API: NoError — манифолд замкнут и корректен.</summary>
    public ManifoldError Status => ManifoldNative.manifold_status(Handle);

    public bool IsEmpty => ManifoldNative.manifold_is_empty(Handle) != 0;

    public bool IsValid => Status == ManifoldError.NoError;

    /// <summary>Род поверхности (тор — 1, сфера — 0); работает только для валидного манифолда.</summary>
    public int Genus => ManifoldNative.manifold_genus(Handle);

    public double Volume => ManifoldNative.manifold_volume(Handle);

    public double SurfaceArea => ManifoldNative.manifold_surface_area(Handle);

    public long VertexCount => (long)ManifoldNative.manifold_num_vert(Handle);

    public long EdgeCount => (long)ManifoldNative.manifold_num_edge(Handle);

    public long TriangleCount => (long)ManifoldNative.manifold_num_tri(Handle);

    // --- конструирование ---

    /// <summary>Меш из массивов (double-точность, MeshGL64) → Manifold.</summary>
    public static Solid FromMesh(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        unsafe
        {
            fixed (double* vp = mesh.Vertices)
            fixed (ulong* tv = mesh.Triangles)
            {
                IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl64();
                IntPtr meshPtr = ManifoldNative.manifold_meshgl64(
                    meshMem, vp, (nuint)mesh.VertexCount, 3, tv, (nuint)mesh.TriangleCount);
                try
                {
                    IntPtr solidMem = ManifoldNative.manifold_alloc_manifold();
                    return new Solid(ManifoldNative.manifold_of_meshgl64(solidMem, meshPtr));
                }
                finally
                {
                    ManifoldNative.manifold_delete_meshgl64(meshPtr);
                }
            }
        }
    }

    /// <summary>Меш из массивов (float-точность, MeshGL) → Manifold. Путь дешевле по трафику, теряет точность.</summary>
    public static Solid FromMeshFloat(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var verts = new float[mesh.Vertices.Length];
        for (int i = 0; i < mesh.Vertices.Length; i++)
            verts[i] = (float)mesh.Vertices[i];
        var tris = new uint[mesh.Triangles.Length];
        for (int i = 0; i < mesh.Triangles.Length; i++)
            tris[i] = checked((uint)mesh.Triangles[i]);

        unsafe
        {
            fixed (float* vp = verts)
            fixed (uint* tv = tris)
            {
                IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl();
                IntPtr meshPtr = ManifoldNative.manifold_meshgl(
                    meshMem, vp, (nuint)mesh.VertexCount, 3, tv, (nuint)mesh.TriangleCount);
                try
                {
                    IntPtr solidMem = ManifoldNative.manifold_alloc_manifold();
                    return new Solid(ManifoldNative.manifold_of_meshgl(solidMem, meshPtr));
                }
                finally
                {
                    ManifoldNative.manifold_delete_meshgl(meshPtr);
                }
            }
        }
    }

    /// <summary>Нативный примитив Manifold::Cube (для перекрёстной проверки с нашими мешами).</summary>
    public static Solid Cube(double sizeX, double sizeY, double sizeZ, bool centered = true)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new Solid(ManifoldNative.manifold_cube(mem, sizeX, sizeY, sizeZ, centered ? 1 : 0));
    }

    /// <summary>Нативный примитив Manifold::Sphere; circularSegments = 0 — значение по умолчанию.</summary>
    public static Solid Sphere(double radius, int circularSegments = 0)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new Solid(ManifoldNative.manifold_sphere(mem, radius, circularSegments));
    }

    /// <summary>
    /// Построение через SDF-колбэк (manifold_level_set_seq): доказательство обратного вызова из C#.
    /// Используется последовательная версия — безопасна для колбэков с managed-стороны.
    /// </summary>
    public static Solid FromSdf(
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
            return new Solid(solid);
        }
        finally
        {
            GC.KeepAlive(fn);
            ManifoldNative.manifold_delete_box(box);
        }
    }

    // --- операции ---

    public Solid Boolean(Solid other, BooleanOp op)
    {
        ArgumentNullException.ThrowIfNull(other);
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new Solid(ManifoldNative.manifold_boolean(mem, Handle, other.Handle, (ManifoldOpType)op));
    }

    public Solid Union(Solid other) => Boolean(other, BooleanOp.Union);

    public Solid Difference(Solid other) => Boolean(other, BooleanOp.Difference);

    public Solid Intersection(Solid other) => Boolean(other, BooleanOp.Intersection);

    public Solid Translate(double x, double y, double z)
    {
        IntPtr mem = ManifoldNative.manifold_alloc_manifold();
        return new Solid(ManifoldNative.manifold_translate(mem, Handle, x, y, z));
    }

    // --- чтение результата обратно в массивы ---

    /// <summary>Результат → массивы double-вершин и ulong-индексов (MeshGL64).</summary>
    public MeshData ToMesh()
    {
        IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl64();
        IntPtr mesh = ManifoldNative.manifold_get_meshgl64(meshMem, Handle);
        try
        {
            nuint nVerts = ManifoldNative.manifold_meshgl64_num_vert(mesh);
            nuint nProps = ManifoldNative.manifold_meshgl64_num_prop(mesh);
            nuint triLen = ManifoldNative.manifold_meshgl64_tri_length(mesh);
            nuint vpLen = ManifoldNative.manifold_meshgl64_vert_properties_length(mesh);
            if (nProps < 3)
                throw new InvalidOperationException($"mesh has {nProps} properties per vertex, expected at least 3 (xyz)");

            var verts = new double[checked((int)(nVerts * 3))];
            var tris = new ulong[checked((int)triLen)];
            unsafe
            {
                void* vpBuf = NativeMemory.Alloc(vpLen, sizeof(double));
                void* tvBuf = NativeMemory.Alloc(triLen, sizeof(ulong));
                try
                {
                    ManifoldNative.manifold_meshgl64_vert_properties((IntPtr)vpBuf, mesh);
                    ManifoldNative.manifold_meshgl64_tri_verts((IntPtr)tvBuf, mesh);
                    double* src = (double*)vpBuf;
                    fixed (double* dst = verts)
                    {
                        for (nuint i = 0; i < nVerts; i++)
                        {
                            dst[i * 3] = src[i * nProps];
                            dst[i * 3 + 1] = src[i * nProps + 1];
                            dst[i * 3 + 2] = src[i * nProps + 2];
                        }
                    }

                    new Span<ulong>((void*)tvBuf, checked((int)triLen)).CopyTo(tris);
                }
                finally
                {
                    NativeMemory.Free(vpBuf);
                    NativeMemory.Free(tvBuf);
                }
            }

            return new MeshData(verts, tris);
        }
        finally
        {
            ManifoldNative.manifold_delete_meshgl64(mesh);
        }
    }

    /// <summary>Результат → массивы через float-путь (MeshGL): вдвое меньше трафика, точность float.</summary>
    public MeshData ToMeshFloat()
    {
        IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl();
        IntPtr mesh = ManifoldNative.manifold_get_meshgl(meshMem, Handle);
        try
        {
            nuint nVerts = ManifoldNative.manifold_meshgl_num_vert(mesh);
            nuint nProps = ManifoldNative.manifold_meshgl_num_prop(mesh);
            nuint nTris = ManifoldNative.manifold_meshgl_num_tri(mesh);
            if (nProps < 3)
                throw new InvalidOperationException($"mesh has {nProps} properties per vertex, expected at least 3 (xyz)");

            nuint vpLen = nVerts * nProps;
            nuint triLen = nTris * 3;
            var verts = new double[checked((int)(nVerts * 3))];
            var tris = new ulong[checked((int)triLen)];
            unsafe
            {
                void* vpBuf = NativeMemory.Alloc(vpLen, sizeof(float));
                void* tvBuf = NativeMemory.Alloc(triLen, sizeof(uint));
                try
                {
                    ManifoldNative.manifold_meshgl_vert_properties((IntPtr)vpBuf, mesh);
                    ManifoldNative.manifold_meshgl_tri_verts((IntPtr)tvBuf, mesh);
                    float* src = (float*)vpBuf;
                    fixed (double* dst = verts)
                    {
                        for (nuint i = 0; i < nVerts; i++)
                        {
                            dst[i * 3] = src[i * nProps];
                            dst[i * 3 + 1] = src[i * nProps + 1];
                            dst[i * 3 + 2] = src[i * nProps + 2];
                        }
                    }

                    uint* srcTri = (uint*)tvBuf;
                    for (nuint i = 0; i < triLen; i++)
                        tris[i] = srcTri[i];
                }
                finally
                {
                    NativeMemory.Free(vpBuf);
                    NativeMemory.Free(tvBuf);
                }
            }

            return new MeshData(verts, tris);
        }
        finally
        {
            ManifoldNative.manifold_delete_meshgl(mesh);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ManifoldNative.manifold_delete_manifold(_handle);
        _handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    ~Solid()
    {
        if (!_disposed && _handle != IntPtr.Zero)
            ManifoldNative.manifold_delete_manifold(_handle);
    }
}
