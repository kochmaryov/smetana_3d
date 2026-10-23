using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Annotations;

/// <summary>Вид размера: выровненный (по отрезку) или линейный (проекция на направление).</summary>
public enum DimensionKind
{
    /// <summary>Расстояние между точками (вдоль отрезка Start–End).</summary>
    Aligned,

    /// <summary>Проекция отрезка на направление <see cref="DimensionNode.MeasureDirection"/>.</summary>
    Linear,
}

/// <summary>
/// Размерная линия (линейная или выровненная): измеряемый отрезок Start–End,
/// направление измерения и смещение размерной линии. Значение хранится
/// в миллиметрах (база сцены, DoD §5.4); подпись в единицах проекта —
/// <see cref="DimensionText.Format"/>.
/// </summary>
public sealed class DimensionNode : AnnotationNode
{
    public DimensionKind Kind { get; set; } = DimensionKind.Aligned;

    /// <summary>Начало измеряемого отрезка (локальные координаты узла, мм).</summary>
    public Vector3d Start { get; set; }

    /// <summary>Конец измеряемого отрезка (локальные координаты узла, мм).</summary>
    public Vector3d End { get; set; }

    /// <summary>
    /// Единичное направление измерения для <see cref="DimensionKind.Linear"/>;
    /// null — доминирующая ось отрезка.
    /// </summary>
    public Vector3d? MeasureDirection { get; set; }

    /// <summary>Вектор смещения размерной линии от измеряемого отрезка (мм).</summary>
    public Vector3d LineOffset { get; set; }

    /// <summary>Своя подпись вместо вычисленного значения; null — вычисленное значение.</summary>
    public string? TextOverride { get; set; }

    /// <summary>Измеренное значение, мм (погрешность ≤ 0,01 мм — DoD §5.4).</summary>
    public double ValueMm => DimensionMath.MeasuredLength(Kind, Start, End, MeasureDirection);
}

/// <summary>
/// Текстовая метка (выноска): <see cref="Anchor"/> — точка на модели,
/// <see cref="LabelPoint"/> — точка, у которой стоит текст.
/// </summary>
public sealed class TextNoteNode : AnnotationNode
{
    /// <summary>Точка выноски на модели (локальные координаты узла, мм).</summary>
    public Vector3d Anchor { get; set; }

    /// <summary>Точка привязки текста (локальные координаты узла, мм).</summary>
    public Vector3d LabelPoint { get; set; }

    /// <summary>Текст метки.</summary>
    public string Text { get; set; } = string.Empty;
}
