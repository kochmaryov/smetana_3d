using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Tools;

/// <summary>Стиль элемента предпросмотра инструмента.</summary>
public enum ToolPreviewStyle
{
    /// <summary>Обычная линия/точка.</summary>
    Default,

    /// <summary>Акцент (деформируемая грань).</summary>
    Highlight,

    /// <summary>Направляющая/связь (склеенные элементы).</summary>
    Guide,

    /// <summary>Предупреждение.</summary>
    Warning,
}

/// <summary>Отрезок предпросмотра в мировых координатах.</summary>
public sealed record ToolPreviewLine(Vector3d Start, Vector3d End, ToolPreviewStyle Style = ToolPreviewStyle.Default);

/// <summary>Точка предпросмотра в мировых координатах.</summary>
public sealed record ToolPreviewPoint(Vector3d Position, ToolPreviewStyle Style = ToolPreviewStyle.Default);

/// <summary>Подпись предпросмотра у мировой точки.</summary>
public sealed record ToolPreviewLabel(Vector3d Anchor, string Text);

/// <summary>
/// Результат инструмента для отрисовки в UI: линии, точки и подписи
/// в мировых координатах (UI проецирует их камерой в пиксели).
/// </summary>
public sealed class ToolPreview
{
    public static readonly ToolPreview Empty = new();

    public ToolPreview(
        IReadOnlyList<ToolPreviewLine>? lines = null,
        IReadOnlyList<ToolPreviewPoint>? points = null,
        IReadOnlyList<ToolPreviewLabel>? labels = null)
    {
        Lines = lines ?? [];
        Points = points ?? [];
        Labels = labels ?? [];
    }

    public IReadOnlyList<ToolPreviewLine> Lines { get; }
    public IReadOnlyList<ToolPreviewPoint> Points { get; }
    public IReadOnlyList<ToolPreviewLabel> Labels { get; }

    public bool IsEmpty => Lines.Count == 0 && Points.Count == 0 && Labels.Count == 0;
}
