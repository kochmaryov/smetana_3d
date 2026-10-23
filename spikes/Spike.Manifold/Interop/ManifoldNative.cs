using System.Runtime.InteropServices;

namespace Spike.Manifold;

/// <summary>
/// Тонкий P/Invoke-слой поверх C API Manifold (MANIFOLD_CBIND): bindings/c/include/manifold/manifoldc.h, v3.5.4.
/// Все функции — cdecl; size_t передаётся как nuint, uint64_t индексов меша — как ulong.
/// </summary>
internal static class ManifoldNative
{
    private const string Lib = "manifoldc";

    // --- память: конструкторы делают placement-new в предоставленный буфер ---
    // (manifold_alloc_* выделяет сырое хранилище через ::operator new;
    //  manifold_delete_* вызывает деструктор + ::operator delete).

    [DllImport(Lib, EntryPoint = "manifold_alloc_manifold", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_alloc_manifold();

    [DllImport(Lib, EntryPoint = "manifold_alloc_meshgl", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_alloc_meshgl();

    [DllImport(Lib, EntryPoint = "manifold_alloc_meshgl64", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_alloc_meshgl64();

    [DllImport(Lib, EntryPoint = "manifold_alloc_box", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_alloc_box();

    [DllImport(Lib, EntryPoint = "manifold_delete_manifold", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void manifold_delete_manifold(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_delete_meshgl", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void manifold_delete_meshgl(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_delete_meshgl64", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void manifold_delete_meshgl64(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_delete_box", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void manifold_delete_box(IntPtr b);

    // --- конструирование мешей и манифолдов ---

    [DllImport(Lib, EntryPoint = "manifold_meshgl", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl(
        IntPtr mem, float* vertProps, nuint nVerts, nuint nProps, uint* triVerts, nuint nTris);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl64(
        IntPtr mem, double* vertProps, nuint nVerts, nuint nProps, ulong* triVerts, nuint nTris);

    [DllImport(Lib, EntryPoint = "manifold_of_meshgl", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_of_meshgl(IntPtr mem, IntPtr mesh);

    [DllImport(Lib, EntryPoint = "manifold_of_meshgl64", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_of_meshgl64(IntPtr mem, IntPtr mesh);

    [DllImport(Lib, EntryPoint = "manifold_cube", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_cube(IntPtr mem, double x, double y, double z, int center);

    [DllImport(Lib, EntryPoint = "manifold_sphere", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_sphere(IntPtr mem, double radius, int circularSegments);

    [DllImport(Lib, EntryPoint = "manifold_box", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_box(
        IntPtr mem, double x1, double y1, double z1, double x2, double y2, double z2);

    // --- булевы операции и трансформации ---

    [DllImport(Lib, EntryPoint = "manifold_boolean", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_boolean(IntPtr mem, IntPtr a, IntPtr b, ManifoldOpType op);

    [DllImport(Lib, EntryPoint = "manifold_union", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_union(IntPtr mem, IntPtr a, IntPtr b);

    [DllImport(Lib, EntryPoint = "manifold_difference", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_difference(IntPtr mem, IntPtr a, IntPtr b);

    [DllImport(Lib, EntryPoint = "manifold_intersection", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_intersection(IntPtr mem, IntPtr a, IntPtr b);

    [DllImport(Lib, EntryPoint = "manifold_translate", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_translate(IntPtr mem, IntPtr m, double x, double y, double z);

    // --- интроспекция и валидация ---

    [DllImport(Lib, EntryPoint = "manifold_status", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern ManifoldError manifold_status(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_is_empty", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int manifold_is_empty(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_num_vert", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_num_vert(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_num_edge", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_num_edge(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_num_tri", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_num_tri(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_genus", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int manifold_genus(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_volume", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern double manifold_volume(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_surface_area", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern double manifold_surface_area(IntPtr m);

    // --- чтение меша обратно (массивы копируются memcpy-ом в буфер вызывающего) ---

    [DllImport(Lib, EntryPoint = "manifold_get_meshgl", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_get_meshgl(IntPtr mem, IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_get_meshgl64", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_get_meshgl64(IntPtr mem, IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl_num_vert", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl_num_vert(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl_num_tri", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl_num_tri(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl_num_prop", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl_num_prop(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl_vert_properties", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl_vert_properties(IntPtr mem, IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl_tri_verts", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl_tri_verts(IntPtr mem, IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_num_vert", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl64_num_vert(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_num_tri", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl64_num_tri(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_num_prop", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl64_num_prop(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_vert_properties_length", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl64_vert_properties_length(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_tri_length", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint manifold_meshgl64_tri_length(IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_vert_properties", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl64_vert_properties(IntPtr mem, IntPtr m);

    [DllImport(Lib, EntryPoint = "manifold_meshgl64_tri_verts", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern unsafe IntPtr manifold_meshgl64_tri_verts(IntPtr mem, IntPtr m);

    // --- SDF-колбэк (ManifoldSdf: double(*)(double, double, double, void*)) ---

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate double SdfFn(double x, double y, double z, IntPtr ctx);

    [DllImport(Lib, EntryPoint = "manifold_level_set_seq", CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr manifold_level_set_seq(
        IntPtr mem, SdfFn sdf, IntPtr bounds, double edgeLength, double level, double tolerance, IntPtr ctx);
}

/// <summary>ManifoldOpType из types.h.</summary>
internal enum ManifoldOpType
{
    Add = 0,
    Subtract = 1,
    Intersect = 2,
}

/// <summary>ManifoldError из types.h.</summary>
public enum ManifoldError
{
    NoError = 0,
    NonFiniteVertex = 1,
    NotManifold = 2,
    VertexIndexOutOfBounds = 3,
    PropertiesWrongLength = 4,
    MissingPositionProperties = 5,
    MergeVectorsDifferentLengths = 6,
    MergeIndexOutOfBounds = 7,
    TransformWrongLength = 8,
    RunIndexWrongLength = 9,
    FaceIdWrongLength = 10,
    InvalidConstruction = 11,
    ResultTooLarge = 12,
    InvalidTangents = 13,
    Cancelled = 14,
}
