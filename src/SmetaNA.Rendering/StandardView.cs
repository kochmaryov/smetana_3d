namespace SmetaNA.Rendering;

/// <summary>
/// Стандартные виды камеры. Мир: Z вверх, X — «красная» ось, Y — «зелёная»
/// (соглашение SketchUp). Вид «спереди» смотрит вдоль +Y, «справа» — с +X.
/// </summary>
public enum StandardView
{
    Front,
    Back,
    Top,
    Bottom,
    Left,
    Right,
    Isometric,
}
