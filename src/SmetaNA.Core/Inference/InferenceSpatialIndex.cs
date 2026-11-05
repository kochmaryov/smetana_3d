using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// BVH по AABB элементов снимка (S11): запрос «что рядом с лучом курсора»
/// возвращает надмножество кандидатов полного перебора, поэтому точные фильтры
/// движка остаются без изменений, а проход по сотням тысяч рёбер заменяется
/// обходом дерева. Отсев консервативный: сфера вокруг AABB узла и границы
/// расстояния до луча, поэтому пропустить кандидата индекс не может.
/// <para>
/// Индекс не зависит от камеры (призма толеранса задаётся лучом), поэтому при
/// движении камеры дерево не перестраивается — только при изменении геометрии.
/// </para>
/// </summary>
internal sealed class InferenceSpatialIndex
{
    private const int LeafSize = 8;

    private readonly Vector3d[] _nodeCenter;
    private readonly double[] _nodeRadius;
    private readonly int[] _nodeLeft;
    private readonly int[] _nodeRight;
    private readonly int[] _nodeStart;
    private readonly int[] _nodeCount;
    private readonly int[] _itemOrder;
    private int _nodeTotal;

    private InferenceSpatialIndex(
        Vector3d[] nodeCenter,
        double[] nodeRadius,
        int[] nodeLeft,
        int[] nodeRight,
        int[] nodeStart,
        int[] nodeCount,
        int[] itemOrder)
    {
        _nodeCenter = nodeCenter;
        _nodeRadius = nodeRadius;
        _nodeLeft = nodeLeft;
        _nodeRight = nodeRight;
        _nodeStart = nodeStart;
        _nodeCount = nodeCount;
        _itemOrder = itemOrder;
    }

    /// <summary>Число индексированных элементов.</summary>
    public int Count => _itemOrder.Length;

    /// <summary>Построить дерево по AABB элементов.</summary>
    public static InferenceSpatialIndex Build(IReadOnlyList<BoundingBox3d> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        int count = boxes.Count;
        var nodeCenter = new Vector3d[Math.Max(1, (2 * count) + 1)];
        var nodeRadius = new double[nodeCenter.Length];
        var nodeLeft = new int[nodeCenter.Length];
        var nodeRight = new int[nodeCenter.Length];
        var nodeStart = new int[nodeCenter.Length];
        var nodeCount = new int[nodeCenter.Length];
        var order = new int[count];
        for (int i = 0; i < count; i++)
        {
            order[i] = i;
        }

        var centroids = new Vector3d[count];
        for (int i = 0; i < count; i++)
        {
            BoundingBox3d box = boxes[i];
            centroids[i] = box.IsEmpty ? Vector3d.Zero : box.Center;
        }

        var index = new InferenceSpatialIndex(
            nodeCenter, nodeRadius, nodeLeft, nodeRight, nodeStart, nodeCount, order);
        if (count > 0)
        {
            index._nodeTotal = 1;
            index.BuildNode(boxes, centroids, node: 0, start: 0, count);
        }

        return index;
    }

    /// <summary>
    /// Элементы, чьи AABB может задеть призма толеранса вокруг луча; в
    /// <paramref name="results"/> добавляются индексы элементов без дубликатов.
    /// </summary>
    /// <param name="ray">Луч курсора.</param>
    /// <param name="radiusPerDepth">Мировой радиус толеранса на единицу глубины (0 — ортопроекция).</param>
    /// <param name="radius">Постоянный радиус призмы (ортопроекция либо 0).</param>
    /// <param name="results">Приёмник индексов элементов (не очищается).</param>
    public void Query(Ray3d ray, double radiusPerDepth, double radius, List<int> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (_itemOrder.Length == 0)
        {
            return;
        }

        Vector3d origin = ray.Origin;
        Vector3d direction = ray.Direction.Normalized();
        Span<int> stack = stackalloc int[128];
        int depth = 0;
        stack[depth++] = 0;
        while (depth > 0)
        {
            int node = stack[--depth];
            if (!Intersects(node, origin, direction, radiusPerDepth, radius))
            {
                continue;
            }

            int count = _nodeCount[node];
            if (count > 0)
            {
                int start = _nodeStart[node];
                for (int i = start; i < start + count; i++)
                {
                    results.Add(_itemOrder[i]);
                }

                continue;
            }

            int left = _nodeLeft[node];
            int right = _nodeRight[node];
            if (left < 0 || right < 0)
            {
                continue;
            }

            if (depth + 2 <= stack.Length)
            {
                stack[depth++] = right;
                stack[depth++] = left;
            }
        }
    }

    private bool Intersects(int node, Vector3d origin, Vector3d direction, double radiusPerDepth, double radius)
    {
        Vector3d offset = _nodeCenter[node] - origin;
        double along = Vector3d.Dot(offset, direction);
        double nodeRadius = _nodeRadius[node];

        // Внутренняя граница расстояния до луча — dist(center) − r, внешняя граница
        // глубины — along + r. Точки за камерой фильтр движка всё равно отбрасывает.
        if (radiusPerDepth > 0 && along + nodeRadius < 0)
        {
            return false;
        }

        double perpendicularSquared = offset.LengthSquared() - (along * along);
        if (perpendicularSquared < 0)
        {
            perpendicularSquared = 0;
        }

        double reach = radius + (radiusPerDepth * (along + nodeRadius)) + nodeRadius;
        return Math.Sqrt(perpendicularSquared) - nodeRadius <= reach;
    }

    private void BuildNode(IReadOnlyList<BoundingBox3d> boxes, Vector3d[] centroids, int node, int start, int count)
    {
        var bounds = BoundingBox3d.Empty;
        for (int i = start; i < start + count; i++)
        {
            bounds = bounds.Union(boxes[_itemOrder[i]]);
        }

        _nodeCenter[node] = bounds.IsEmpty ? Vector3d.Zero : bounds.Center;
        _nodeRadius[node] = bounds.IsEmpty ? 0 : bounds.DiagonalLength * 0.5;

        if (count <= LeafSize)
        {
            _nodeStart[node] = start;
            _nodeCount[node] = count;
            _nodeLeft[node] = -1;
            _nodeRight[node] = -1;
            return;
        }

        Vector3d size = bounds.Size;
        int axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
        int half = count / 2;
        NthElement(_itemOrder, centroids, start, count, start + half, axis);

        int left = _nodeTotal++;
        int right = _nodeTotal++;
        _nodeLeft[node] = left;
        _nodeRight[node] = right;
        _nodeCount[node] = 0;
        _nodeStart[node] = 0;
        BuildNode(boxes, centroids, left, start, half);
        BuildNode(boxes, centroids, right, start + half, count - half);
    }

    /// <summary>Частичное упорядочивание: элемент <paramref name="nth"/> встаёт на своё место (quickselect).</summary>
    private static void NthElement(int[] order, Vector3d[] centroids, int start, int count, int nth, int axis)
    {
        int left = start;
        int right = start + count - 1;
        while (left < right)
        {
            double pivot = AxisOf(centroids[order[(left + right) / 2]], axis);
            int i = left;
            int j = right;
            while (i <= j)
            {
                while (AxisOf(centroids[order[i]], axis) < pivot)
                {
                    i++;
                }

                while (AxisOf(centroids[order[j]], axis) > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    (order[i], order[j]) = (order[j], order[i]);
                    i++;
                    j--;
                }
            }

            if (nth <= j)
            {
                right = j;
            }
            else if (nth >= i)
            {
                left = i;
            }
            else
            {
                return;
            }
        }
    }

    private static double AxisOf(Vector3d point, int axis) => axis switch
    {
        0 => point.X,
        1 => point.Y,
        _ => point.Z,
    };
}
