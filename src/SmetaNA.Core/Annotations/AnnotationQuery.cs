using SmetaNA.Core.Model;

namespace SmetaNA.Core.Annotations;

/// <summary>
/// Поиск аннотаций в иерархии сцены: аннотации живут узлами сцены
/// (см. <see cref="AnnotationNode"/>), поэтому находятся обычным обходом.
/// </summary>
public static class AnnotationQuery
{
    /// <summary>Все аннотации сцены; скрытые узлы включаются по <paramref name="includeHidden"/>.</summary>
    public static IReadOnlyList<AnnotationNode> FindInScene(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var result = new List<AnnotationNode>();
        foreach (OutlinerEntry entry in OutlinerQuery.Walk(scene))
        {
            if (!includeHidden && !entry.IsEffectivelyVisible)
            {
                continue;
            }

            if (entry.Node is AnnotationNode annotation)
            {
                result.Add(annotation);
            }
        }

        return result;
    }

    /// <summary>Размеры сцены.</summary>
    public static IReadOnlyList<DimensionNode> FindDimensions(Scene scene, bool includeHidden = false)
    {
        var result = new List<DimensionNode>();
        foreach (AnnotationNode annotation in FindInScene(scene, includeHidden))
        {
            if (annotation is DimensionNode dimension)
            {
                result.Add(dimension);
            }
        }

        return result;
    }

    /// <summary>Текстовые метки сцены.</summary>
    public static IReadOnlyList<TextNoteNode> FindTextNotes(Scene scene, bool includeHidden = false)
    {
        var result = new List<TextNoteNode>();
        foreach (AnnotationNode annotation in FindInScene(scene, includeHidden))
        {
            if (annotation is TextNoteNode note)
            {
                result.Add(note);
            }
        }

        return result;
    }
}
