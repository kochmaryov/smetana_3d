using System.Runtime.InteropServices;
using SmetaNA.Core.Geometry;
using SmetaNA.Manifold.Interop;

namespace SmetaNA.Manifold;

/// <summary>
/// Мост конвертации HalfEdgeMesh/MeshData ↔ Manifold (спик 0.4: HalfEdgeMesh → MeshData →
/// Solid.FromMesh и обратно). Обмен — MeshGL64 (double): Manifold внутри всегда double,
/// float-путь теряет точность и для геометрического ядра не используется.
/// </summary>
/// <remarks>
/// <see cref="ToSolid(MeshData)"/> не бросает исключение на невалидном меше — диагностика
/// C API доступна через <see cref="ManifoldSolid.Status"/> (NoError — меш замкнут/2-манифолден;
/// пустой меш — валидный пустой манифолд). Обратная конвертация бросает
/// <see cref="ManifoldException"/>, если тело невалидно.
/// </remarks>
public static class ManifoldBridge
{
    /// <summary>Треугольный суп → Manifold (ingest, MeshGL64; обход CCW снаружи).</summary>
    public static ManifoldSolid ToSolid(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ValidateIndices(mesh);

        int vertexCount = mesh.Positions.Count;
        int indexCount = mesh.Indices.Count;
        var verts = new double[checked(vertexCount * 3)];
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3d p = mesh.Positions[i];
            verts[i * 3] = p.X;
            verts[i * 3 + 1] = p.Y;
            verts[i * 3 + 2] = p.Z;
        }

        var tris = new ulong[indexCount];
        for (int i = 0; i < indexCount; i++)
        {
            tris[i] = (ulong)mesh.Indices[i];
        }

        unsafe
        {
            fixed (double* vp = verts)
            fixed (ulong* tv = tris)
            {
                IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl64();
                IntPtr meshPtr = ManifoldNative.manifold_meshgl64(
                    meshMem, vp, (nuint)vertexCount, 3, tv, (nuint)(indexCount / 3));
                try
                {
                    IntPtr solidMem = ManifoldNative.manifold_alloc_manifold();
                    return new ManifoldSolid(ManifoldNative.manifold_of_meshgl64(solidMem, meshPtr), materialized: true);
                }
                finally
                {
                    ManifoldNative.manifold_delete_meshgl64(meshPtr);
                }
            }
        }
    }

    /// <summary>Полумногоугольниковый меш → Manifold (через треугольный суп, веерная триангуляция граней).</summary>
    public static ManifoldSolid ToSolid(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return ToSolid(mesh.ToTriangles());
    }

    /// <summary>Manifold → треугольный суп (readback MeshGL64, double).</summary>
    /// <exception cref="ManifoldException">Тело невалидно (статус C API не NoError).</exception>
    public static MeshData ToMeshData(ManifoldSolid solid, IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solid);
        solid.EnsureMaterialized(progress, cancellationToken);
        IntPtr handle = solid.Handle;

        ManifoldError status = ManifoldNative.manifold_status(handle);
        if (status != ManifoldError.NoError)
        {
            throw new ManifoldException(status, $"CSG-результат невалиден: {status}.");
        }

        if (ManifoldNative.manifold_is_empty(handle) != 0)
        {
            return new MeshData(Array.Empty<Vector3d>(), Array.Empty<int>());
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(CsgProgress.For(CsgStage.Readback));

        IntPtr meshMem = ManifoldNative.manifold_alloc_meshgl64();
        IntPtr mesh = ManifoldNative.manifold_get_meshgl64(meshMem, handle);
        try
        {
            nuint nVerts = ManifoldNative.manifold_meshgl64_num_vert(mesh);
            nuint nProps = ManifoldNative.manifold_meshgl64_num_prop(mesh);
            nuint triLen = ManifoldNative.manifold_meshgl64_tri_length(mesh);
            nuint vpLen = ManifoldNative.manifold_meshgl64_vert_properties_length(mesh);
            if (nProps < 3)
            {
                throw new InvalidOperationException($"Меш имеет {nProps} свойств на вершину, ожидалось минимум 3 (xyz).");
            }

            var positions = new Vector3d[checked((int)nVerts)];
            var indices = new int[checked((int)triLen)];
            unsafe
            {
                void* vpBuf = NativeMemory.Alloc(vpLen, sizeof(double));
                void* tvBuf = NativeMemory.Alloc(triLen, sizeof(ulong));
                try
                {
                    ManifoldNative.manifold_meshgl64_vert_properties((IntPtr)vpBuf, mesh);
                    ManifoldNative.manifold_meshgl64_tri_verts((IntPtr)tvBuf, mesh);
                    double* src = (double*)vpBuf;
                    for (nuint i = 0; i < nVerts; i++)
                    {
                        positions[i] = new Vector3d(src[i * nProps], src[i * nProps + 1], src[i * nProps + 2]);
                    }

                    ulong* srcTri = (ulong*)tvBuf;
                    for (nuint i = 0; i < triLen; i++)
                    {
                        indices[i] = checked((int)srcTri[i]);
                    }
                }
                finally
                {
                    NativeMemory.Free(vpBuf);
                    NativeMemory.Free(tvBuf);
                }
            }

            return new MeshData(positions, indices);
        }
        finally
        {
            ManifoldNative.manifold_delete_meshgl64(mesh);
        }
    }

    /// <summary>Manifold → полумногоугольниковый меш; пустое тело даёт пустой меш.</summary>
    /// <exception cref="ManifoldException">Тело невалидно (статус C API не NoError).</exception>
    public static HalfEdgeMesh ToHalfEdgeMesh(ManifoldSolid solid, IProgress<CsgProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        MeshData mesh = ToMeshData(solid, progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(CsgProgress.For(CsgStage.RebuildMesh));
        return HalfEdgeMesh.FromTriangles(mesh.Positions, mesh.Indices);
    }

    private static void ValidateIndices(MeshData mesh)
    {
        int vertexCount = mesh.Positions.Count;
        if (mesh.Indices.Count % 3 != 0)
        {
            throw new ArgumentException("Число индексов должно быть кратно 3.", nameof(mesh));
        }

        IReadOnlyList<int> indices = mesh.Indices;
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (idx < 0 || idx >= vertexCount)
            {
                throw new ArgumentException($"Индекс вершины {idx} вне диапазона ({vertexCount} вершин).", nameof(mesh));
            }
        }
    }
}
