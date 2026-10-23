using System.Diagnostics.CodeAnalysis;

namespace SmetaNA.Core.Materials;

/// <summary>
/// Палитра материалов меша: упорядоченный список, индексы в котором — «адреса»
/// материалов для <c>MeshFace.MaterialIndex</c>. Значения материалов сравниваются
/// по значению: <see cref="IndexOfOrAdd"/> не создаёт дублей одинаковых материалов.
/// </summary>
public sealed class MaterialTable
{
    private readonly List<Material> _materials = [];

    /// <summary>Материалы палитры в порядке индексов.</summary>
    public IReadOnlyList<Material> Materials => _materials;

    public int Count => _materials.Count;

    public Material this[int index] => _materials[index];

    /// <summary>Добавить материал; возвращает его индекс.</summary>
    public int Add(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        _materials.Add(material);
        return _materials.Count - 1;
    }

    /// <summary>Материал по индексу; <c>false</c> — индекс вне палитры.</summary>
    public bool TryGet(int index, [NotNullWhen(true)] out Material? material)
    {
        if (index < 0 || index >= _materials.Count)
        {
            material = null;
            return false;
        }

        material = _materials[index];
        return true;
    }

    /// <summary>Индекс равного материала или -1.</summary>
    public int IndexOf(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return _materials.IndexOf(material);
    }

    /// <summary>Индекс равного материала; если такого нет — материал добавляется.</summary>
    public int IndexOfOrAdd(Material material)
    {
        int index = IndexOf(material);
        return index >= 0 ? index : Add(material);
    }
}
