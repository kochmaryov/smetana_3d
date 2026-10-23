using SmetaNA.Core.Geometry;
using SmetaNA.Core.Undo;

namespace SmetaNA.Core.Tools.Commands;

/// <summary>
/// Контур фигуры рисования: точки (вершины) в координатах меша, признак
/// замкнутости и создание грани на замкнутом контуре (если он плоский).
/// </summary>
/// <param name="Points">Вершины контура (минимум 2; для замкнутого — 3).</param>
/// <param name="IsClosed">Контур замкнут (последняя точка соединяется с первой).</param>
/// <param name="CreateFace">Создать грань на замкнутом контуре.</param>
public sealed record DrawingContour(IReadOnlyList<Vector3d> Points, bool IsClosed, bool CreateFace);

/// <summary>Фигура рисования: набор контуров (одного обычно достаточно).</summary>
public sealed record DrawingShapeSpec(IReadOnlyList<DrawingContour> Contours)
{
    /// <summary>Незамкнутая ломаная (линия, дуга, штрих).</summary>
    public static DrawingShapeSpec Open(IReadOnlyList<Vector3d> points)
        => new([new DrawingContour(points, false, false)]);

    /// <summary>Замкнутый контур (прямоугольник, окружность, многоугольник).</summary>
    public static DrawingShapeSpec Closed(IReadOnlyList<Vector3d> ring, bool createFace = true)
        => new([new DrawingContour(ring, true, createFace)]);

    /// <summary>Фигура с точками, преобразованными трансформацией.</summary>
    public DrawingShapeSpec Transform(Transform3d transform)
    {
        var contours = new List<DrawingContour>(Contours.Count);
        foreach (DrawingContour contour in Contours)
        {
            var points = new List<Vector3d>(contour.Points.Count);
            foreach (Vector3d point in contour.Points)
            {
                points.Add(transform.TransformPoint(point));
            }

            contours.Add(new DrawingContour(points, contour.IsClosed, contour.CreateFace));
        }

        return new DrawingShapeSpec(contours);
    }
}

/// <summary>
/// Команда истории: добавление нарисованной фигуры (проволочные рёбра и,
/// для замкнутых плоских контуров, грань) в меш. <see cref="Undo"/> удаляет
/// созданные вершины и рёбра — меш возвращается в исходное состояние,
/// <c>Redo</c> строит фигуру заново.
/// </summary>
public sealed class AddDrawingShapeCommand : IEditCommand
{
    private readonly HalfEdgeMesh _mesh;
    private readonly DrawingShapeSpec _spec;
    private List<BuiltContour>? _built;

    public AddDrawingShapeCommand(HalfEdgeMesh mesh, DrawingShapeSpec spec, string description)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Validate(spec);
        _mesh = mesh;
        _spec = spec;
        Description = description;
    }

    public string Description { get; }

    /// <summary>Построенные элементы последнего <see cref="Execute"/>.</summary>
    public IReadOnlyList<BuiltContour> Built => _built ?? [];

    public void Execute()
    {
        if (_built is not null)
        {
            throw new InvalidOperationException("Команда уже выполнена: сначала отмените её.");
        }

        var built = new List<BuiltContour>(_spec.Contours.Count);
        foreach (DrawingContour contour in _spec.Contours)
        {
            var vertices = new List<MeshVertex>(contour.Points.Count);
            foreach (Vector3d point in contour.Points)
            {
                vertices.Add(_mesh.AddVertex(point));
            }

            var forwards = new List<MeshHalfEdge>(contour.IsClosed ? vertices.Count : vertices.Count - 1);
            for (int i = 0; i + 1 < vertices.Count; i++)
            {
                forwards.Add(_mesh.AddWireEdge(vertices[i], vertices[i + 1]).Forward);
            }

            if (contour.IsClosed)
            {
                forwards.Add(_mesh.AddWireEdge(vertices[^1], vertices[0]).Forward);
            }

            MeshFace? face = contour.CreateFace ? _mesh.FillLoop(forwards) : null;
            built.Add(new BuiltContour(vertices, forwards, face));
        }

        _built = built;
    }

    public void Undo()
    {
        if (_built is not { } built)
        {
            throw new InvalidOperationException("Команда ещё не выполнялась.");
        }

        for (int i = built.Count - 1; i >= 0; i--)
        {
            BuiltContour contour = built[i];
            if (contour.Face is { } face)
            {
                _mesh.DetachFace(face);
            }

            for (int j = contour.ForwardEdges.Count - 1; j >= 0; j--)
            {
                _mesh.RemoveWireEdge(contour.ForwardEdges[j]);
            }

            for (int j = contour.Vertices.Count - 1; j >= 0; j--)
            {
                _mesh.RemoveVertex(contour.Vertices[j]);
            }
        }

        _built = null;
    }

    /// <summary>Построенный контур: вершины, «прямые» полуребра и грань (если создавалась).</summary>
    public sealed record BuiltContour(
        IReadOnlyList<MeshVertex> Vertices,
        IReadOnlyList<MeshHalfEdge> ForwardEdges,
        MeshFace? Face);

    private static void Validate(DrawingShapeSpec spec)
    {
        if (spec.Contours.Count == 0)
        {
            throw new ArgumentException("Фигура не содержит контуров.", nameof(spec));
        }

        foreach (DrawingContour contour in spec.Contours)
        {
            int minimum = contour.IsClosed ? 3 : 2;
            if (contour.Points.Count < minimum)
            {
                throw new ArgumentException($"Контур требует минимум {minimum} точек.", nameof(spec));
            }

            if (contour.CreateFace && !contour.IsClosed)
            {
                throw new ArgumentException("Грань создаётся только на замкнутом контуре.", nameof(spec));
            }

            if (contour.CreateFace && !DrawingShapes.IsPlanar(contour.Points))
            {
                throw new ArgumentException("Грань требует плоского невырожденного контура.", nameof(spec));
            }

            foreach (Vector3d point in contour.Points)
            {
                if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
                {
                    throw new ArgumentException("Контур содержит нечисловые координаты.", nameof(spec));
                }
            }

            for (int i = 0; i < contour.Points.Count; i++)
            {
                Vector3d from = contour.Points[i];
                Vector3d to = contour.Points[(i + 1) % contour.Points.Count];
                if (i + 1 == contour.Points.Count && !contour.IsClosed)
                {
                    break;
                }

                if ((to - from).Length() <= 0)
                {
                    throw new ArgumentException("Контур содержит нулевой сегмент.", nameof(spec));
                }
            }
        }
    }
}
