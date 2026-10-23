using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Annotations;

/// <summary>
/// База аннотаций (модуль M4): размеры и текстовые метки живут в сцене как узлы —
/// наследуют <see cref="SceneNode"/>, поэтому участвуют в иерархии (Outliner), тегах,
/// видимости и undo/redo через штатные команды узлов. Геометрия аннотации хранится
/// в локальных координатах узла и масштабируется вместе с его трансформацией.
/// </summary>
public abstract class AnnotationNode : SceneNode
{
    /// <summary>
    /// Мировая трансформация узла: локальные трансформации предков применяются
    /// после локальной трансформации самого узла (соглашение <see cref="Transform3d.Multiply"/>).
    /// </summary>
    public Transform3d WorldTransform()
    {
        Transform3d world = LocalTransform;
        for (SceneNode? parent = Parent; parent is not null; parent = parent.Parent)
        {
            world = Transform3d.Multiply(world, parent.LocalTransform);
        }

        return world;
    }
}
