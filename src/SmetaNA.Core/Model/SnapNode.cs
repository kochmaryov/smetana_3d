using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Model;

/// <summary>
/// Пользовательская точка сборки (Snap, v1.x): ориентированный диск внутри группы
/// или компонента. Кадр хранится в локальных координатах узла, поэтому snap
/// переезжает вместе с контейнером; при привязке берётся <see cref="WorldFrame"/>.
/// </summary>
public sealed class SnapNode : SceneNode
{
    /// <summary>Кадр snap в локальных координатах узла (нормаль диска и up).</summary>
    public SnapFrame Frame { get; set; } = SnapFrame.World;

    /// <summary>Кадр в мировых координатах с учётом трансформации узла.</summary>
    public SnapFrame WorldFrame(Transform3d worldTransform) => Frame.Transformed(worldTransform);
}
