namespace SmetaNA.Core.Tools;

/// <summary>
/// Пространство координат числового ввода (R-16, S14): мировое или локальное
/// (оси активного контекста — выбранного контейнера).
/// </summary>
public enum CoordinateSpace
{
    /// <summary>Мировые координаты сцены.</summary>
    World,

    /// <summary>Координаты контекста: оси и начало выбранного контейнера.</summary>
    Local,
}
