namespace SmetaNA.Rendering;

/// <summary>Режим проекции камеры.</summary>
public enum ProjectionMode
{
    /// <summary>Перспектива (оптический зум — через расстояние до цели).</summary>
    Perspective,

    /// <summary>Ортография (масштаб — через высоту видимой области OrthoHeight).</summary>
    Orthographic,
}
