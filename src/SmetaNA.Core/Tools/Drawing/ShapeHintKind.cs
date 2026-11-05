namespace SmetaNA.Core.Tools.Drawing;

/// <summary>
/// Подсказка формы при построении прямоугольника (shape inference, R-35):
/// пропорция доведена до точного значения, а не задана пользователем.
/// </summary>
public enum ShapeHintKind
{
    /// <summary>Обычный прямоугольник без подсказки.</summary>
    None,

    /// <summary>Пропорция доведена до квадрата.</summary>
    Square,

    /// <summary>Пропорция доведена до золотого сечения.</summary>
    GoldenRatio,
}
