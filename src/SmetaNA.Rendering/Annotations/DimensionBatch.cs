using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering.Annotations;

/// <summary>Одна подпись текста: якорь и диапазон её штрихов в общем массиве сегментов.</summary>
/// <param name="Text">Текст подписи.</param>
/// <param name="Anchor">Мировая точка привязки текста.</param>
/// <param name="FirstSegment">Первый сегмент штрихов текста в <see cref="DimensionBatch.Vertices"/>.</param>
/// <param name="SegmentCount">Число сегментов штрихов текста.</param>
public sealed record DimensionTextRun(string Text, Vector3d Anchor, int FirstSegment, int SegmentCount);

/// <summary>Диапазон сегментов одного элемента батча (размера/метки).</summary>
/// <param name="NodeId">Идентификатор узла-аннотации.</param>
/// <param name="FirstSegment">Первый сегмент элемента.</param>
/// <param name="SegmentCount">Число сегментов элемента.</param>
/// <param name="Text">Подпись элемента (значение размера/текст метки) или null.</param>
public sealed record DimensionItemRange(Guid NodeId, int FirstSegment, int SegmentCount, string? Text);

/// <summary>
/// Батч отрисовки размеров и меток (задача M4): мировые сегменты линий —
/// размерные линии, выноски, стрелки и штрихи текста — в одном массиве
/// (6 float на сегмент: x0,y0,z0,x1,y1,z1), один draw-вызов GL_LINES.
/// Текстовые прогоны и элементы ссылаются на диапазоны сегментов.
/// </summary>
public sealed class DimensionBatch
{
    /// <summary>Пустой батч.</summary>
    public static DimensionBatch Empty { get; } = new()
    {
        Vertices = [],
        TextRuns = [],
        Items = [],
    };

    /// <summary>Вершины сегментов: 6 float на сегмент (начало, конец), мировые координаты, мм.</summary>
    public required float[] Vertices { get; init; }

    /// <summary>Текстовые прогоны (штрихи каждого текста).</summary>
    public required IReadOnlyList<DimensionTextRun> TextRuns { get; init; }

    /// <summary>Диапазоны сегментов по элементам (размерам/меткам).</summary>
    public required IReadOnlyList<DimensionItemRange> Items { get; init; }

    /// <summary>Число сегментов линий.</summary>
    public int SegmentCount => Vertices.Length / 6;

    public bool IsEmpty => SegmentCount == 0;
}

/// <summary>Накопитель сегментов и диапазонов батча размеров.</summary>
public sealed class DimensionBatchBuilder
{
    private readonly List<float> _vertices = [];
    private readonly List<DimensionTextRun> _textRuns = [];
    private readonly List<DimensionItemRange> _items = [];

    /// <summary>Число уже добавленных сегментов.</summary>
    public int SegmentCount => _vertices.Count / 6;

    /// <summary>Добавить сегмент линии (мировые координаты, мм).</summary>
    public void AddSegment(Vector3d from, Vector3d to)
    {
        _vertices.Add((float)from.X);
        _vertices.Add((float)from.Y);
        _vertices.Add((float)from.Z);
        _vertices.Add((float)to.X);
        _vertices.Add((float)to.Y);
        _vertices.Add((float)to.Z);
    }

    /// <summary>Зарегистрировать текстовый прогон (его штрихи уже добавлены сегментами).</summary>
    public void AddTextRun(string text, Vector3d anchor, int firstSegment, int segmentCount)
        => _textRuns.Add(new DimensionTextRun(text, anchor, firstSegment, segmentCount));

    /// <summary>Закрыть элемент батча, начатый на сегменте <paramref name="firstSegment"/>.</summary>
    public void EndItem(Guid nodeId, string? text, int firstSegment)
        => _items.Add(new DimensionItemRange(nodeId, firstSegment, SegmentCount - firstSegment, text));

    public DimensionBatch Build() => new()
    {
        Vertices = [.. _vertices],
        TextRuns = [.. _textRuns],
        Items = [.. _items],
    };
}
