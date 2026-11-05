using SmetaNA.Core.Geometry;
using SmetaNA.Core.Model;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Построение снимка сцены для inference из иерархии <see cref="Scene"/>:
/// мировые рёбра (уникальные неориентированные рёбра мешей), грани (кольца граней)
/// и окружности с учётом трансформаций, групп и инстансов компонентов.
/// Тесселяции окружностей/дуг инструментов рисования не сохраняют семантику
/// кривой, поэтому центр/нормаль/радиус восстанавливаются circle-fit'ом по
/// вершинам колец граней и проволочных цепей (см. <see cref="TryAddCircle"/>);
/// строящиеся кривые инструменты добавляют в <see cref="InferenceScene"/> напрямую.
/// </summary>
public static class InferenceSceneBuilder
{
    /// <summary>Снимок всех видимых мешей сцены.</summary>
    public static InferenceScene FromScene(Scene scene, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return FromNode(scene.Root, Transform3d.Identity, includeHidden);
    }

    /// <summary>
    /// Снимок поддерева узла с начальной трансформацией; содержимое определений
    /// компонентов разворачивается по одному разу на каждый инстанс.
    /// </summary>
    public static InferenceScene FromNode(SceneNode root, Transform3d rootTransform, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(root);

        var segments = new List<SnapSegment>();
        var faces = new List<SnapFace>();
        var circles = new List<SnapCircle>();
        var arcPoints = new List<SnapArcPoint>();
        var arcTangents = new List<SnapArcTangent>();
        var instanceOrigins = new List<SnapInstanceOrigin>();
        var userSnaps = new List<SnapUserPoint>();
        CollectNode(
            root,
            rootTransform,
            includeHidden,
            [],
            containerName: null,
            isRoot: true,
            segments,
            faces,
            circles,
            arcPoints,
            arcTangents,
            instanceOrigins,
            userSnaps);
        return new InferenceScene(
            segments, faces, circles, arcPoints, instanceOrigins, userSnaps, arcTangents);
    }

    private static void CollectNode(
        SceneNode node,
        Transform3d parentWorld,
        bool includeHidden,
        HashSet<Guid> openDefinitions,
        string? containerName,
        bool isRoot,
        List<SnapSegment> segments,
        List<SnapFace> faces,
        List<SnapCircle> circles,
        List<SnapArcPoint> arcPoints,
        List<SnapArcTangent> arcTangents,
        List<SnapInstanceOrigin> instanceOrigins,
        List<SnapUserPoint> userSnaps)
    {
        if (!includeHidden && !node.IsVisible)
        {
            return;
        }

        Transform3d world = Transform3d.Multiply(node.LocalTransform, parentWorld);

        string? name = containerName;
        if (!isRoot && node is GroupNode or ComponentInstanceNode)
        {
            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                name = node.Name;
            }

            Vector3d axisX = world.TransformVector(Vector3d.UnitX);
            Vector3d axisY = world.TransformVector(Vector3d.UnitY);
            Vector3d axisZ = world.TransformVector(Vector3d.UnitZ);
            if (axisX.LengthSquared() > 0 && axisY.LengthSquared() > 0 && axisZ.LengthSquared() > 0)
            {
                instanceOrigins.Add(new SnapInstanceOrigin(
                    world.TransformPoint(Vector3d.Zero),
                    axisX,
                    axisY,
                    axisZ,
                    new SnapEntityRef(
                        node.Id,
                        node is ComponentInstanceNode ? SnapEntityKind.ComponentInstance : SnapEntityKind.Node,
                        null,
                        node.Name)));
            }
        }

        if (node is MeshNode meshNode)
        {
            CollectMesh(
                meshNode.Mesh,
                world,
                meshNode.Id,
                name ?? meshNode.Name,
                segments,
                faces,
                circles,
                arcPoints,
                arcTangents);
        }

        if (node is SnapNode snapNode)
        {
            userSnaps.Add(new SnapUserPoint(
                snapNode.WorldFrame(world),
                new SnapEntityRef(snapNode.Id, SnapEntityKind.UserSnap, null, name ?? snapNode.Name)));
        }

        foreach (SceneNode child in EnumerateContent(node, openDefinitions))
        {
            CollectNode(
                child,
                world,
                includeHidden,
                openDefinitions,
                name,
                isRoot: false,
                segments,
                faces,
                circles,
                arcPoints,
                arcTangents,
                instanceOrigins,
                userSnaps);
        }
    }

    private static void CollectMesh(
        HalfEdgeMesh mesh,
        Transform3d world,
        Guid nodeId,
        string? displayName,
        List<SnapSegment> segments,
        List<SnapFace> faces,
        List<SnapCircle> circles,
        List<SnapArcPoint> arcPoints,
        List<SnapArcTangent> arcTangents)
    {
        var indexOf = new Dictionary<MeshVertex, int>(mesh.Vertices.Count);
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            indexOf[mesh.Vertices[i]] = i;
        }

        var seenEdges = new HashSet<(int From, int To)>();
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            MeshVertex origin = edge.Origin;
            MeshVertex destination = edge.Next!.Origin;
            int i = indexOf[origin];
            int j = indexOf[destination];
            var key = i < j ? (i, j) : (j, i);
            if (!seenEdges.Add(key))
            {
                continue;
            }

            Vector3d start = world.TransformPoint(origin.Position);
            Vector3d end = world.TransformPoint(destination.Position);
            if ((end - start).LengthSquared() <= 0)
            {
                continue;
            }

            segments.Add(new SnapSegment(start, end)
            {
                Entity = new SnapEntityRef(nodeId, SnapEntityKind.Edge, segments.Count, displayName),
            });
        }

        foreach (MeshFace face in mesh.Faces)
        {
            var ring = new List<Vector3d>();
            foreach (MeshHalfEdge edge in MeshTraversal.EnumerateRing(face))
            {
                Vector3d position = world.TransformPoint(edge.Origin.Position);
                if (ring.Count == 0 || (position - ring[^1]).LengthSquared() > 0)
                {
                    ring.Add(position);
                }
            }

            if (ring.Count > 1 && (ring[0] - ring[^1]).LengthSquared() <= 0)
            {
                ring.RemoveAt(ring.Count - 1);
            }

            if (ring.Count < 3 || ComputeNormalMagnitude(ring) <= 0)
            {
                continue;
            }

            faces.Add(new SnapFace(ring)
            {
                Entity = new SnapEntityRef(nodeId, SnapEntityKind.Face, faces.Count, displayName),
            });
            TryAddCircle(ring, closed: true, circles, new SnapEntityRef(nodeId, SnapEntityKind.Circle, circles.Count, displayName));
        }

        CollectWireCircles(mesh, world, indexOf, nodeId, displayName, circles, arcPoints, arcTangents);
    }

    /// <summary>
    /// Окружности проволочных цепей (дуги и незалитые контуры): каждая
    /// компонента связности рёбер без граней проверяется на лежачие на общей
    /// окружности вершины (компоненты с ветвлением пропускаются: это не кривая).
    /// </summary>
    private static void CollectWireCircles(
        HalfEdgeMesh mesh,
        Transform3d world,
        Dictionary<MeshVertex, int> indexOf,
        Guid nodeId,
        string? displayName,
        List<SnapCircle> circles,
        List<SnapArcPoint> arcPoints,
        List<SnapArcTangent> arcTangents)
    {
        var neighbors = new Dictionary<int, List<int>>();
        var seen = new HashSet<(int From, int To)>();
        foreach (MeshHalfEdge edge in mesh.HalfEdges)
        {
            if (edge.Face is not null || edge.Twin is { Face: not null })
            {
                continue;
            }

            int i = indexOf[edge.Origin];
            int j = indexOf[edge.Next!.Origin];
            if (i == j)
            {
                continue;
            }

            var key = i < j ? (i, j) : (j, i);
            if (!seen.Add(key))
            {
                continue;
            }

            if (!neighbors.TryGetValue(i, out List<int>? from))
            {
                neighbors[i] = from = [];
            }

            if (!neighbors.TryGetValue(j, out List<int>? to))
            {
                neighbors[j] = to = [];
            }

            from.Add(j);
            to.Add(i);
        }

        var visited = new HashSet<int>();
        foreach (int seed in neighbors.Keys)
        {
            if (!visited.Add(seed))
            {
                continue;
            }

            var component = new List<int> { seed };
            var stack = new Stack<int>();
            stack.Push(seed);
            int degreeSum = 0;
            while (stack.Count > 0)
            {
                int vertex = stack.Pop();
                List<int> linked = neighbors[vertex];
                degreeSum += linked.Count;
                foreach (int next in linked)
                {
                    if (visited.Add(next))
                    {
                        component.Add(next);
                        stack.Push(next);
                    }
                }
            }

            // Ветвление — это не одиночная кривая: компонента уже целиком
            // помечена посещённой, пропускаем её без circle-fit'а.
            bool branching = false;
            foreach (int index in component)
            {
                if (neighbors[index].Count > 2)
                {
                    branching = true;
                    break;
                }
            }

            if (branching)
            {
                continue;
            }

            bool closed = degreeSum / 2 == component.Count;
            var points = new List<Vector3d>(component.Count);
            foreach (int index in component)
            {
                points.Add(world.TransformPoint(mesh.Vertices[index].Position));
            }

            if (!TryFitCircle(points, closed, out Vector3d center, out Vector3d normal, out double radius))
            {
                continue;
            }

            int circleIndex = circles.Count;
            var entity = new SnapEntityRef(nodeId, SnapEntityKind.Circle, circleIndex, displayName);
            circles.Add(new SnapCircle(center, normal, radius) { Entity = entity });

            if (!closed
                && OrderOpenChain(component, neighbors, mesh, world) is { } ordered
                && TryArcMidpoint(ordered, out Vector3d arcMidpoint))
            {
                arcPoints.Add(new SnapArcPoint(arcMidpoint, entity));
                AddArcTangents(ordered, center, normal, entity, arcTangents);
            }
        }
    }

    /// <summary>
    /// Касательные в концевых вершинах открытой дуги (R-34): касательная
    /// окружности в точке вершины — <c>normal × radial</c>.
    /// </summary>
    private static void AddArcTangents(
        List<Vector3d> ordered,
        Vector3d center,
        Vector3d normal,
        SnapEntityRef entity,
        List<SnapArcTangent> arcTangents)
    {
        if (ordered.Count < 2)
        {
            return;
        }

        foreach (Vector3d vertex in (Vector3d[])[ordered[0], ordered[^1]])
        {
            Vector3d radial = vertex - center;
            Vector3d direction = Vector3d.Cross(normal, radial);
            if (direction.LengthSquared() <= 0)
            {
                continue;
            }

            arcTangents.Add(new SnapArcTangent(vertex, direction.Normalized(), entity));
        }
    }

    /// <summary>Упорядочивание вершин открытой цепи от одного конца к другому.</summary>
    private static List<Vector3d>? OrderOpenChain(
        List<int> component,
        Dictionary<int, List<int>> neighbors,
        HalfEdgeMesh mesh,
        Transform3d world)
    {
        int start = -1;
        foreach (int index in component)
        {
            if (neighbors[index].Count == 1)
            {
                start = index;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var ordered = new List<Vector3d>(component.Count);
        var visited = new HashSet<int>();
        int previous = -1;
        int current = start;
        while (visited.Add(current))
        {
            ordered.Add(world.TransformPoint(mesh.Vertices[current].Position));

            int next = -1;
            foreach (int candidate in neighbors[current])
            {
                if (candidate != previous && !visited.Contains(candidate))
                {
                    next = candidate;
                    break;
                }
            }

            if (next < 0)
            {
                break;
            }

            previous = current;
            current = next;
        }

        return ordered.Count == component.Count ? ordered : null;
    }

    /// <summary>Точка на середине полилинии по длине (середина дуги, а не хорды).</summary>
    private static bool TryArcMidpoint(IReadOnlyList<Vector3d> points, out Vector3d midpoint)
    {
        midpoint = default;
        if (points.Count < 2)
        {
            return false;
        }

        double total = 0;
        for (int i = 1; i < points.Count; i++)
        {
            total += (points[i] - points[i - 1]).Length();
        }

        if (total <= 0)
        {
            return false;
        }

        double half = total * 0.5;
        double walked = 0;
        for (int i = 1; i < points.Count; i++)
        {
            Vector3d from = points[i - 1];
            Vector3d to = points[i];
            double length = (to - from).Length();
            if (length <= 0)
            {
                continue;
            }

            if (walked + length >= half)
            {
                midpoint = from + (to - from) * ((half - walked) / length);
                return true;
            }

            walked += length;
        }

        midpoint = points[^1];
        return true;
    }

    /// <summary>
    /// Circle-fit: три разнесённые точки задают окружность, все остальные
    /// обязаны лежать на ней с относительным допуском. Минимум точек отсекает
    /// ложные срабатывания: любые три точки лежат на окружности (открытая цепь
    /// требует 4+ вершин), а углы прямоугольника/треугольника всегда вписаны в
    /// окружность, но центром «кривой» не являются (кольцо — 5+). Ограничение
    /// радиуса размахом точек отсекает почти прямые цепи — «окружность»
    /// огромного радиуса кривой не является.
    /// </summary>
    private static void TryAddCircle(
        IReadOnlyList<Vector3d> points,
        bool closed,
        List<SnapCircle> circles,
        SnapEntityRef entity)
    {
        if (TryFitCircle(points, closed, out Vector3d center, out Vector3d normal, out double radius))
        {
            circles.Add(new SnapCircle(center, normal, radius) { Entity = entity });
        }
    }

    private static bool TryFitCircle(
        IReadOnlyList<Vector3d> points,
        bool closed,
        out Vector3d center,
        out Vector3d normal,
        out double radius)
    {
        center = default;
        normal = default;
        radius = 0;

        int minimum = closed ? 5 : 4;
        if (points.Count < minimum)
        {
            return false;
        }

        // Три разнесённые точки: первая, самая дальняя от неё и самая дальшая
        // от прямой через них (на хорду она не ложится — вырождение исключено).
        Vector3d a = points[0];
        Vector3d b = a;
        double spanSquared = 0;
        foreach (Vector3d point in points)
        {
            double distanceSquared = (point - a).LengthSquared();
            if (distanceSquared > spanSquared)
            {
                spanSquared = distanceSquared;
                b = point;
            }
        }

        if (spanSquared <= 0)
        {
            return false;
        }

        Vector3d chord = b - a;
        Vector3d c = a;
        double heightSquared = 0;
        foreach (Vector3d point in points)
        {
            double height = Vector3d.Cross(point - a, chord).LengthSquared();
            if (height > heightSquared)
            {
                heightSquared = height;
                c = point;
            }
        }

        if (heightSquared <= 0
            || !DrawingShapes.TryCircumcircle(a, b, c, out Vector3d fittedCenter, out Vector3d fittedNormal, out double fittedRadius))
        {
            return false;
        }

        if (fittedRadius > 100.0 * Math.Sqrt(spanSquared))
        {
            return false;
        }

        double tolerance = Math.Max(1e-9, fittedRadius * 1e-6);
        foreach (Vector3d point in points)
        {
            if (Math.Abs((point - fittedCenter).Length() - fittedRadius) > tolerance)
            {
                return false;
            }
        }

        center = fittedCenter;
        normal = fittedNormal;
        radius = fittedRadius;
        return true;
    }

    private static double ComputeNormalMagnitude(IReadOnlyList<Vector3d> ring)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3d current = ring[i];
            Vector3d next = ring[(i + 1) % ring.Count];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z).Length();
    }

    /// <summary>
    /// Собственные дети узла плюс содержимое определения для инстанса компонента
    /// (циклы компонентов обнаруживаются и приводят к исключению, как в
    /// <see cref="SceneFlattener"/>).
    /// </summary>
    private static IEnumerable<SceneNode> EnumerateContent(SceneNode node, HashSet<Guid> openDefinitions)
    {
        if (node is ComponentInstanceNode instance)
        {
            ComponentDefinition definition = instance.Definition;
            if (!openDefinitions.Add(definition.Id))
            {
                throw new InvalidOperationException($"Циклическая ссылка на компонент «{definition.Name}» в иерархии сцены.");
            }

            yield return definition.Content;

            openDefinitions.Remove(definition.Id);
        }

        foreach (SceneNode child in node.Children)
        {
            yield return child;
        }
    }
}
