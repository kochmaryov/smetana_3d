using SmetaNA.Core.Annotations;
using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;

namespace SmetaNA.Rendering.Annotations;

/// <summary>
/// Построение батча размеров и меток (модуль M4): мировая геометрия размерных
/// линий, выносок, стрелок и штрихового текста. Подписи и стрелки постоянны по
/// размеру на экране — мировой масштаб пикселя берётся из
/// <see cref="InferenceViewport.WorldRadiusAt"/> в точке привязки.
/// <para>
/// Текст — штриховой рендер <see cref="StrokeFont"/> в плоскости камеры
/// (billboard) без шрифтовых движков ОС: данные батча полностью проверяемы без
/// GL и рисуются тем же line-проходом, что и линии размеров.
/// </para>
/// </summary>
public static class DimensionRenderer
{
    /// <summary>Насколько кончик стрелки длиннее её «крыльев».</summary>
    private const double ArrowWingRatio = 0.35;

    /// <summary>Батч по аннотациям сцены (в порядке обхода).</summary>
    public static DimensionBatch Build(
        IReadOnlyList<AnnotationNode> annotations,
        InferenceViewport viewport,
        LengthUnit unit,
        DimensionStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(viewport);

        style ??= DimensionStyle.Default;
        var builder = new DimensionBatchBuilder();
        foreach (AnnotationNode annotation in annotations)
        {
            switch (annotation)
            {
                case DimensionNode dimension:
                    AppendDimension(builder, dimension, viewport, unit, style);
                    break;
                case TextNoteNode note:
                    AppendTextNote(builder, note, viewport, style);
                    break;
            }
        }

        return builder.Build();
    }

    private static void AppendDimension(
        DimensionBatchBuilder builder,
        DimensionNode dimension,
        InferenceViewport viewport,
        LengthUnit unit,
        DimensionStyle style)
    {
        Transform3d world = dimension.WorldTransform();
        Vector3d start = world.TransformPoint(dimension.Start);
        Vector3d end = world.TransformPoint(dimension.End);
        Vector3d? direction = dimension.MeasureDirection is { } localDirection
            ? world.TransformVector(localDirection).Normalized()
            : null;
        Vector3d measureDirection = DimensionMath.DirectionOf(start, end, direction);
        Vector3d measuredEnd = DimensionMath.MeasuredEnd(dimension.Kind, start, end, direction);
        Vector3d offset = world.TransformVector(dimension.LineOffset);

        Vector3d lineStart = start + offset;
        Vector3d lineEnd = measuredEnd + offset;
        Vector3d line = lineEnd - lineStart;
        double length = DimensionMath.MeasuredLength(dimension.Kind, start, end, direction);
        string text = DimensionText.Format(length, unit, dimension.TextOverride);

        int firstSegment = builder.SegmentCount;
        if (!TryPixelScale(viewport, (lineStart + lineEnd) * 0.5, out double pixelScale))
        {
            builder.EndItem(dimension.Id, text, firstSegment);
            return;
        }

        Vector3d lineDirection = line.LengthSquared() > 0 ? line.Normalized() : measureDirection;
        Vector3d offsetDirection = offset.LengthSquared() > 0
            ? offset.Normalized()
            : DefaultPerpendicular(lineDirection, viewport.Forward);

        // Выносные линии: от измеряемых точек до размерной линии с вылетом.
        double overshoot = style.ExtensionOvershootPx * pixelScale;
        if (offset.LengthSquared() > 0)
        {
            builder.AddSegment(start, lineStart + offsetDirection * overshoot);
            builder.AddSegment(measuredEnd, lineEnd + offsetDirection * overshoot);
        }

        // Размерная линия.
        if (line.LengthSquared() > 0)
        {
            builder.AddSegment(lineStart, lineEnd);
        }

        // Стрелки на концах размерной линии (в плоскости экрана).
        if (line.LengthSquared() > 0)
        {
            double arrowLength = style.ArrowSizePx * pixelScale;
            Vector3d wing = Vector3d.Cross(lineDirection, viewport.Forward);
            if (wing.LengthSquared() > 0)
            {
                wing = wing.Normalized() * (arrowLength * ArrowWingRatio);
            }

            Vector3d arrow = lineDirection * arrowLength;
            builder.AddSegment(lineStart, lineStart + arrow + wing);
            builder.AddSegment(lineStart, lineStart + arrow - wing);
            builder.AddSegment(lineEnd, lineEnd - arrow + wing);
            builder.AddSegment(lineEnd, lineEnd - arrow - wing);
        }

        // Подпись: штриховой текст над серединой размерной линии.
        Vector3d anchor = (lineStart + lineEnd) * 0.5;
        AppendText(
            builder,
            text,
            anchor,
            viewport,
            style,
            pixelScale,
            centerHorizontally: true,
            (style.TextGapPx + style.TextHeightPx) * pixelScale);

        builder.EndItem(dimension.Id, text, firstSegment);
    }

    private static void AppendTextNote(
        DimensionBatchBuilder builder,
        TextNoteNode note,
        InferenceViewport viewport,
        DimensionStyle style)
    {
        Transform3d world = note.WorldTransform();
        Vector3d anchorPoint = world.TransformPoint(note.Anchor);
        Vector3d labelPoint = world.TransformPoint(note.LabelPoint);

        int firstSegment = builder.SegmentCount;
        if (!TryPixelScale(viewport, labelPoint, out double pixelScale))
        {
            builder.EndItem(note.Id, note.Text, firstSegment);
            return;
        }

        // Выноска: линия к модели и засечка в точке привязки.
        builder.AddSegment(anchorPoint, labelPoint);
        Vector3d leader = labelPoint - anchorPoint;
        if (leader.LengthSquared() > 0)
        {
            Vector3d tick = DefaultPerpendicular(leader.Normalized(), viewport.Forward) * (style.ExtensionOvershootPx * pixelScale);
            builder.AddSegment(anchorPoint - tick, anchorPoint + tick);
        }

        AppendText(
            builder,
            note.Text,
            labelPoint,
            viewport,
            style,
            pixelScale,
            centerHorizontally: false,
            style.TextGapPx * pixelScale);

        builder.EndItem(note.Id, note.Text, firstSegment);
    }

    /// <summary>
    /// Штрихи текста в плоскости камеры: базовая линия — от якоря вверх на
    /// <paramref name="baselineRise"/>, начало — по центру или слева.
    /// </summary>
    private static void AppendText(
        DimensionBatchBuilder builder,
        string text,
        Vector3d anchor,
        InferenceViewport viewport,
        DimensionStyle style,
        double pixelScale,
        bool centerHorizontally,
        double baselineRise)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        double heightWorld = style.TextHeightPx * pixelScale;
        double widthWorld = StrokeFont.MeasureWidth(text) * heightWorld;
        Vector3d origin = anchor + viewport.Up * baselineRise;
        if (centerHorizontally)
        {
            origin -= viewport.Right * (widthWorld * 0.5);
        }

        int firstSegment = builder.SegmentCount;
        foreach ((Vector2d from, Vector2d to) in StrokeFont.BuildStrokes(text, heightWorld))
        {
            builder.AddSegment(
                origin + viewport.Right * from.X + viewport.Up * from.Y,
                origin + viewport.Right * to.X + viewport.Up * to.Y);
        }

        builder.AddTextRun(text, anchor, firstSegment, builder.SegmentCount - firstSegment);
    }

    /// <summary>Мировой масштаб пикселя в точке; false — точка не имеет масштаба (за камерой).</summary>
    private static bool TryPixelScale(InferenceViewport viewport, Vector3d point, out double pixelScale)
    {
        if (viewport.Projection == InferenceProjection.Perspective && viewport.DepthOf(point) <= 0)
        {
            pixelScale = 0;
            return false;
        }

        pixelScale = viewport.WorldRadiusAt(point, 1.0);
        return double.IsFinite(pixelScale) && pixelScale > 0;
    }

    /// <summary>Перпендикуляр к направлению в плоскости экрана.</summary>
    private static Vector3d DefaultPerpendicular(Vector3d direction, Vector3d viewForward)
    {
        Vector3d perpendicular = Vector3d.Cross(direction, viewForward);
        if (perpendicular.LengthSquared() > 0)
        {
            return perpendicular.Normalized();
        }

        perpendicular = Vector3d.Cross(direction, Vector3d.UnitZ);
        return perpendicular.LengthSquared() > 0 ? perpendicular.Normalized() : Vector3d.UnitX;
    }
}
