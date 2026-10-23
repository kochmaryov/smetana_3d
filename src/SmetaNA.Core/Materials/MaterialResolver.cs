using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Materials;

/// <summary>
/// Разрешение индексов материалов меша: грань наследует материал объекта
/// (<c>HalfEdgeMesh.DefaultMaterialIndex</c>), если у неё нет явного индекса;
/// индексы указывают в палитру <c>HalfEdgeMesh.Materials</c>.
/// </summary>
public static class MaterialResolver
{
    /// <summary>
    /// Эффективный индекс материала грани: явный индекс грани, иначе — материал
    /// меша по умолчанию; -1 — материал не назначен.
    /// </summary>
    public static int EffectiveIndex(HalfEdgeMesh mesh, MeshFace face)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(face);
        return face.MaterialIndex >= 0 ? face.MaterialIndex : mesh.DefaultMaterialIndex;
    }

    /// <summary>
    /// Материал грани: по эффективному индексу из палитры меша; если индекса нет
    /// или он вне палитры — <see cref="MaterialPalette.Default"/>.
    /// </summary>
    public static Material Resolve(HalfEdgeMesh mesh, MeshFace face)
    {
        int index = EffectiveIndex(mesh, face);
        return mesh.Materials.TryGet(index, out Material? material) ? material : MaterialPalette.Default;
    }

    /// <summary>Есть ли в меше хоть одно назначение материала (грань или объект).</summary>
    public static bool HasAnyAssignment(HalfEdgeMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.DefaultMaterialIndex >= 0)
        {
            return true;
        }

        foreach (MeshFace face in mesh.Faces)
        {
            if (face.MaterialIndex >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
