using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Rendering.Annotations;
using Xunit;

namespace SmetaNA.Rendering.Tests;

/// <summary>
/// Рендер-батчинг размеров: проверка данных батча (линии, стрелки, выноски,
/// штрихи текста и диапазоны) без GL.
/// </summary>
public sealed class DimensionBatchTests
{
    private static InferenceViewport TopDownViewport() =>
        InferenceViewport.CreateOrthographic(
            new Vector3d(50, 10, 500), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), 200, 800, 600);

    private static DimensionNode HorizontalDimension(double length = 100) => new()
    {
        Kind = DimensionKind.Aligned,
        Start = new Vector3d(0, 0, 0),
        End = new Vector3d(length, 0, 0),
        LineOffset = new Vector3d(0, 20, 0),
    };

    private static bool HasSegment(DimensionBatch batch, Vector3d from, Vector3d to, double tolerance = 1e-3)
    {
        for (int i = 0; i < batch.Vertices.Length; i += 6)
        {
            var a = new Vector3d(batch.Vertices[i], batch.Vertices[i + 1], batch.Vertices[i + 2]);
            var b = new Vector3d(batch.Vertices[i + 3], batch.Vertices[i + 4], batch.Vertices[i + 5]);
            if (Near(a, from, tolerance) && Near(b, to, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSegmentEndingAt(DimensionBatch batch, Vector3d from, double tolerance = 1e-3)
    {
        for (int i = 0; i < batch.Vertices.Length; i += 6)
        {
            var a = new Vector3d(batch.Vertices[i], batch.Vertices[i + 1], batch.Vertices[i + 2]);
            if (Near(a, from, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Near(Vector3d a, Vector3d b, double tolerance)
        => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance && Math.Abs(a.Z - b.Z) <= tolerance;

    [Fact]
    public void Build_SingleDimension_BatchesLinesArrowsAndText()
    {
        DimensionNode dimension = HorizontalDimension();
        DimensionBatch batch = DimensionRenderer.Build([dimension], TopDownViewport(), LengthUnit.Millimeter);

        DimensionItemRange item = Assert.Single(batch.Items);
        Assert.Equal(dimension.Id, item.NodeId);
        Assert.Equal("100,00 mm", item.Text);
        Assert.Equal(0, item.FirstSegment);

        DimensionTextRun run = Assert.Single(batch.TextRuns);
        Assert.Equal("100,00 mm", run.Text);
        Assert.True(Near(run.Anchor, new Vector3d(50, 20, 0), 1e-3), $"якорь {run.Anchor}");
        Assert.True(run.SegmentCount > 0);
        Assert.Equal(7 + run.SegmentCount, batch.SegmentCount);

        // Размерная линия, выносные линии и хотя бы одна стрелка.
        Assert.True(HasSegment(batch, new Vector3d(0, 20, 0), new Vector3d(100, 20, 0)), "нет размерной линии");
        Assert.True(HasSegmentEndingAt(batch, new Vector3d(0, 0, 0)), "нет выносной линии");
        Assert.True(HasSegmentEndingAt(batch, new Vector3d(100, 0, 0)), "нет второй выносной линии");
        Assert.Equal(item.SegmentCount, batch.SegmentCount);

        foreach (float value in batch.Vertices)
        {
            Assert.True(float.IsFinite(value), "вершина батча не конечна");
        }
    }

    [Fact]
    public void Build_TextRunSegments_FollowItemRanges()
    {
        DimensionNode dimension = HorizontalDimension();
        DimensionBatch batch = DimensionRenderer.Build([dimension], TopDownViewport(), LengthUnit.Millimeter);

        DimensionTextRun run = Assert.Single(batch.TextRuns);
        Assert.Equal(7, run.FirstSegment);
        Assert.Equal(batch.SegmentCount - 7, run.SegmentCount);
    }

    [Fact]
    public void Build_UnitControlsText()
    {
        DimensionNode dimension = HorizontalDimension();
        DimensionBatch centimeters = DimensionRenderer.Build([dimension], TopDownViewport(), LengthUnit.Centimeter);
        Assert.Equal("10,000 cm", Assert.Single(centimeters.TextRuns).Text);

        dimension.TextOverride = "100 мм";
        DimensionBatch overriden = DimensionRenderer.Build([dimension], TopDownViewport(), LengthUnit.Millimeter);
        Assert.Equal("100 мм", Assert.Single(overriden.TextRuns).Text);
    }

    [Fact]
    public void Build_DimensionAndTextNote_TwoItemsWithLeader()
    {
        DimensionNode dimension = HorizontalDimension();
        var note = new TextNoteNode
        {
            Anchor = new Vector3d(20, 10, 0),
            LabelPoint = new Vector3d(40, 60, 0),
            Text = "wall1",
        };

        DimensionBatch batch = DimensionRenderer.Build([dimension, note], TopDownViewport(), LengthUnit.Millimeter);

        Assert.Equal(2, batch.Items.Count);
        Assert.Equal(2, batch.TextRuns.Count);
        Assert.Equal("wall1", batch.TextRuns[1].Text);
        Assert.True(HasSegment(batch, new Vector3d(20, 10, 0), new Vector3d(40, 60, 0)), "нет выноски");
    }

    [Fact]
    public void Build_AnnotationBehindCamera_IsSkippedWithoutThrow()
    {
        var behind = new DimensionNode
        {
            Start = new Vector3d(0, 0, 600),
            End = new Vector3d(100, 0, 600),
            LineOffset = new Vector3d(0, 20, 0),
        };
        InferenceViewport perspective = InferenceViewport.CreatePerspective(
            new Vector3d(50, 10, 500), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), Math.PI / 4, 800, 600);

        DimensionBatch batch = DimensionRenderer.Build([behind], perspective, LengthUnit.Millimeter);

        DimensionItemRange item = Assert.Single(batch.Items);
        Assert.Equal(0, item.SegmentCount);
        Assert.Empty(batch.TextRuns);
    }

    [Fact]
    public void Build_NoAnnotations_YieldsEmptyBatch()
    {
        DimensionBatch batch = DimensionRenderer.Build([], TopDownViewport(), LengthUnit.Millimeter);

        Assert.True(batch.IsEmpty);
        Assert.Empty(batch.Items);
        Assert.Empty(batch.TextRuns);
    }

    [Fact]
    public void Builder_CollectsSegmentsAndRanges()
    {
        var builder = new DimensionBatchBuilder();
        int first = builder.SegmentCount;
        builder.AddSegment(Vector3d.Zero, new Vector3d(1, 0, 0));
        builder.AddSegment(new Vector3d(1, 0, 0), new Vector3d(1, 1, 0));
        builder.AddTextRun("1", new Vector3d(0.5, 0, 0), first, 2);
        builder.EndItem(Guid.NewGuid(), "1", first);

        DimensionBatch batch = builder.Build();

        Assert.Equal(2, batch.SegmentCount);
        Assert.Equal(12, batch.Vertices.Length);
        Assert.Single(batch.TextRuns);
        Assert.Single(batch.Items);
    }
}
