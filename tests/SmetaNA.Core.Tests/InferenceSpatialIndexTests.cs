using SmetaNA.Core.Geometry;
using SmetaNA.Core.Inference;
using Xunit;

namespace SmetaNA.Core.Tests;

/// <summary>
/// S11: BVH по AABB обязан возвращать надмножество кандидатов полного перебора —
/// иначе привязки на больших сценах начнут «терять» геометрию.
/// </summary>
public sealed class InferenceSpatialIndexTests
{
    [Fact]
    public void Query_ReturnsSupersetOfBruteForcePrefilter()
    {
        var random = new Random(20261105);
        const int count = 4000;
        var segments = new SnapSegment[count];
        for (int i = 0; i < count; i++)
        {
            Vector3d start = RandomPoint(random);
            Vector3d end = start + (RandomPoint(random) - new Vector3d(200, 200, 200)) * 0.1;
            segments[i] = new SnapSegment(start, end);
        }

        var boxes = new BoundingBox3d[count];
        for (int i = 0; i < count; i++)
        {
            boxes[i] = BoundingBox3d.Empty.Union(segments[i].Start).Union(segments[i].End);
        }

        InferenceSpatialIndex index = InferenceSpatialIndex.Build(boxes);
        var hits = new List<int>();

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var viewport = InferenceViewport.CreatePerspective(
                RandomPoint(random),
                SafeDirection(RandomPoint(random) - new Vector3d(200, 200, 200)),
                Vector3d.UnitZ,
                Math.PI / 3,
                800,
                600);
            Vector2d cursor = new(random.NextDouble() * 800, random.NextDouble() * 600);
            Ray3d ray = viewport.PickRay(cursor);
            const double tolerancePx = 8;
            const double margin = 2;
            double perDepth = viewport.WorldRadiusAt(viewport.EyePosition + ray.Direction.Normalized(), tolerancePx)
                * margin;

            hits.Clear();
            index.Query(ray, perDepth, 0, hits);
            var hitSet = new HashSet<int>(hits);

            Vector3d direction = ray.Direction.Normalized();
            for (int i = 0; i < count; i++)
            {
                if (!MatchesPrefilter(segments[i], ray.Origin, direction, perDepth))
                {
                    continue;
                }

                Assert.True(
                    hitSet.Contains(i),
                    $"Сегмент {i} проходит предфильтр, но не попал в выборку индекса (попытка {attempt}).");
            }
        }
    }

    [Fact]
    public void Query_Orthographic_PrunesDistantSegments()
    {
        const int far = 32;
        var segments = new List<SnapSegment>
        {
            new(new Vector3d(-10, 0, 0), new Vector3d(10, 0, 0)),
        };
        for (int i = 1; i <= far; i++)
        {
            double y = 100.0 * i;
            segments.Add(new SnapSegment(new Vector3d(-10, y, 0), new Vector3d(10, y, 0)));
        }

        var boxes = segments
            .Select(segment => BoundingBox3d.Empty.Union(segment.Start).Union(segment.End))
            .ToArray();

        InferenceSpatialIndex index = InferenceSpatialIndex.Build(boxes);
        var viewport = InferenceViewport.CreateOrthographic(
            new Vector3d(0, 0, 100), new Vector3d(0, 0, -1), new Vector3d(0, 1, 0), 100, 800, 800);
        Ray3d ray = viewport.PickRay(new Vector2d(400, 400));

        var hits = new List<int>();
        index.Query(ray, 0, viewport.WorldRadiusAt(ray.Origin, 8) * 2, hits);

        Assert.Contains(0, hits);

        // Отсев идёт по листьям, поэтому допустим возврат одного листа (до 8 элементов),
        // но дальние листья должны быть отсечены — иначе индекс бесполезен.
        Assert.True(
            hits.Count <= 8,
            $"Индекс вернул {hits.Count} из 33 сегментов: отсев по листьям не работает.");
    }

    private static bool MatchesPrefilter(SnapSegment segment, Vector3d origin, Vector3d direction, double perDepth)
    {
        double margin = segment.Length * 0.5;
        Vector3d point = segment.Midpoint;
        double depth = Vector3d.Dot(point - origin, direction);
        if (depth <= 0)
        {
            return false;
        }

        Vector3d offset = point - origin;
        double along = Vector3d.Dot(offset, direction);
        double perpendicularSquared = offset.LengthSquared() - (along * along);
        if (perpendicularSquared < 0)
        {
            perpendicularSquared = 0;
        }

        double radius = (perDepth * depth) + margin;
        return perpendicularSquared <= radius * radius;
    }

    private static Vector3d RandomPoint(Random random) => new(
        random.NextDouble() * 400,
        random.NextDouble() * 400,
        random.NextDouble() * 400);

    private static Vector3d SafeDirection(Vector3d direction)
        => direction.LengthSquared() > 1e-9 ? direction.Normalized() : Vector3d.UnitZ;
}
