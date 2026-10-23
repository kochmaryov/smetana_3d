using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>
/// Inference-движок: по лучу выбора, снимку сцены, направляющим и модификаторам
/// выдаёт упорядоченные кандидаты привязок (задачи 2.4 и 2.6).
/// <para>
/// Порядок кандидатов: по убыванию приоритета (endpoint выше midpoint — см.
/// <see cref="SnapDefaults"/>), затем по убыванию уверенности (ближе к курсору),
/// затем по глубине (ближе к камере). Толеранс задаётся в пикселях экрана и
/// переводится в мировые единицы параметрами <see cref="InferenceViewport"/>.
/// </para>
/// <para>
/// Комбинированные привязки (точка + линейное ограничение) помечаются маркером
/// <see cref="SnapMarkerKind.LineAndPoint"/>, несут направление и вторичную точку
/// (якорь), их приоритет = приоритет точки + 1.
/// </para>
/// <para>
/// Движок чистый и без состояния: весь UI (отрисовка маркеров, VCB) — снаружи.
/// </para>
/// </summary>
public static class InferenceEngine
{
    /// <summary>Кандидаты привязок для запроса, отсортированные по убыванию предпочтительности.</summary>
    public static SnapResult Solve(SnapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Viewport);
        ArgumentNullException.ThrowIfNull(request.Scene);
        ArgumentNullException.ThrowIfNull(request.Guides);

        if (!double.IsFinite(request.TolerancePx) || request.TolerancePx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Толеранс в пикселях должен быть положительным.");
        }

        return new Solver(request).Solve();
    }

    private sealed class Solver
    {
        private readonly SnapRequest _request;
        private readonly InferenceViewport _viewport;
        private readonly Vector2d _cursor;
        private readonly double _tolerancePx;
        private readonly Ray3d _ray;
        private readonly Vector3d? _anchor;
        private readonly bool _pointsAllowed;
        private readonly HashSet<SnapType> _allowedLinear;

        private readonly List<SnapCandidate> _pointCandidates = [];
        private readonly List<SnapCandidate> _linearCandidates = [];

        public Solver(SnapRequest request)
        {
            _request = request;
            _viewport = request.Viewport;
            _cursor = request.CursorPx;
            _tolerancePx = request.TolerancePx;
            _ray = request.Ray;
            _anchor = request.Anchor;
            ResolveModifiers(request.Modifiers, out _pointsAllowed, out _allowedLinear);
        }

        public SnapResult Solve()
        {
            if (_pointsAllowed)
            {
                CollectPointCandidates();
            }

            if (_allowedLinear.Count > 0)
            {
                CollectLinearCandidates();
                CollectOnFaceAlongDirections();
                PromoteCombined();
            }

            List<SnapCandidate> accepted = [];
            accepted.AddRange(_pointCandidates);
            accepted.AddRange(_linearCandidates);
            accepted.RemoveAll(c => (SnapDefaults.FilterOf(c.Type) & _request.Filter) == 0);

            List<SnapCandidate> ordered = accepted
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.Confidence)
                .ThenBy(c => _viewport.DepthOf(c.Point))
                .ThenBy(c => c.ScreenDistancePx)
                .ToList();

            return new SnapResult(Dedupe(ordered));
        }

        private void CollectPointCandidates()
        {
            SnapFilter filter = _request.Filter;
            InferenceScene scene = _request.Scene;

            if ((filter & SnapFilter.Origin) != 0)
            {
                TryAddPoint(SnapType.Origin, Vector3d.Zero);
            }

            if ((filter & SnapFilter.Endpoints) != 0)
            {
                foreach (SnapSegment segment in scene.Segments)
                {
                    TryAddPoint(SnapType.Endpoint, segment.Start);
                    TryAddPoint(SnapType.Endpoint, segment.End);
                }
            }

            if ((filter & SnapFilter.Midpoints) != 0)
            {
                foreach (SnapSegment segment in scene.Segments)
                {
                    TryAddPoint(SnapType.Midpoint, segment.Midpoint);
                }
            }

            if ((filter & SnapFilter.Centers) != 0)
            {
                foreach (SnapCircle circle in scene.Circles)
                {
                    TryAddPoint(SnapType.Center, circle.Center);
                }

                foreach (SnapFace face in scene.Faces)
                {
                    TryAddPoint(SnapType.Center, face.Centroid);
                }
            }

            if ((filter & SnapFilter.Guides) != 0)
            {
                foreach (SnapGuide guide in _request.Guides)
                {
                    if (InferenceMath.TryClosestPointOnLineToRay(_ray, guide.Point, guide.Direction, out Vector3d point))
                    {
                        TryAddPoint(SnapType.Guide, point);
                    }
                }
            }

            if ((filter & SnapFilter.Intersections) != 0)
            {
                CollectIntersections();
            }

            if ((filter & SnapFilter.OnFace) != 0)
            {
                foreach (SnapFace face in scene.Faces)
                {
                    if (face.TryIntersect(_ray, out Vector3d hit) && face.Contains(hit, WorldTolerance(hit)))
                    {
                        TryAddPoint(SnapType.OnFace, hit);
                    }
                }
            }
        }

        private void CollectIntersections()
        {
            IReadOnlyList<SnapSegment> segments = _request.Scene.Segments;
            IReadOnlyList<SnapGuide> guides = _request.Guides;

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    SnapSegment a = segments[i];
                    SnapSegment b = segments[j];
                    if (SharesEndpoint(a, b))
                    {
                        continue;
                    }

                    if (InferenceMath.TryIntersectSegmentSegment(
                        a.Start, a.End, b.Start, b.End, WorldTolerance(a.Midpoint), out Vector3d point))
                    {
                        TryAddPoint(SnapType.Intersection, point);
                    }
                }

                foreach (SnapGuide guide in guides)
                {
                    SnapSegment segment = segments[i];
                    if (InferenceMath.TryIntersectSegmentLine(
                        segment.Start, segment.End, guide.Point, guide.Direction,
                        WorldTolerance(segment.Midpoint), out Vector3d point))
                    {
                        TryAddPoint(SnapType.Intersection, point);
                    }
                }
            }

            for (int i = 0; i < guides.Count; i++)
            {
                for (int j = i + 1; j < guides.Count; j++)
                {
                    if (InferenceMath.TryIntersectLines(
                        guides[i].Point, guides[i].Direction,
                        guides[j].Point, guides[j].Direction,
                        WorldTolerance(guides[i].Point), out Vector3d point))
                    {
                        TryAddPoint(SnapType.Intersection, point);
                    }
                }
            }
        }

        private void CollectLinearCandidates()
        {
            Vector3d basis = _anchor ?? Vector3d.Zero;

            if (_allowedLinear.Contains(SnapType.AxisX))
            {
                TryAddLinear(SnapType.AxisX, basis, Vector3d.UnitX);
            }

            if (_allowedLinear.Contains(SnapType.AxisY))
            {
                TryAddLinear(SnapType.AxisY, basis, Vector3d.UnitY);
            }

            if (_allowedLinear.Contains(SnapType.AxisZ))
            {
                TryAddLinear(SnapType.AxisZ, basis, Vector3d.UnitZ);
            }

            if (_anchor is not Vector3d anchor)
            {
                return;
            }

            if (_allowedLinear.Contains(SnapType.Parallel))
            {
                foreach (Vector3d direction in EnumerateReferenceDirections())
                {
                    TryAddLinear(SnapType.Parallel, anchor, direction);
                }
            }

            if (_allowedLinear.Contains(SnapType.Perpendicular))
            {
                foreach (SnapSegment segment in _request.Scene.Segments)
                {
                    TryAddPerpendicular(segment.Start, segment.Direction, anchor);
                }

                foreach (SnapGuide guide in _request.Guides)
                {
                    TryAddPerpendicular(guide.Point, guide.Direction, anchor);
                }
            }

            if (_allowedLinear.Contains(SnapType.Tangent))
            {
                foreach (SnapCircle circle in _request.Scene.Circles)
                {
                    double planeTolerance = WorldTolerance(circle.Center);
                    if (InferenceMath.TryTangentPoints(
                        anchor, circle.Center, circle.Normal, circle.Radius, planeTolerance,
                        out Vector3d first, out Vector3d second))
                    {
                        TryAddTangent(first, anchor);
                        TryAddTangent(second, anchor);
                    }
                }
            }
        }

        private void CollectOnFaceAlongDirections()
        {
            if (_anchor is not Vector3d anchor)
            {
                return;
            }

            foreach ((SnapType type, Vector3d direction) in EnumerateConstraintDirections())
            {
                foreach (SnapFace face in _request.Scene.Faces)
                {
                    double denominator = Vector3d.Dot(direction, face.Normal);
                    if (Math.Abs(denominator) <= 1e-12)
                    {
                        continue;
                    }

                    double t = Vector3d.Dot(face.Ring[0] - anchor, face.Normal) / denominator;
                    Vector3d point = anchor + direction * t;
                    if ((point - anchor).LengthSquared() <= 0)
                    {
                        continue;
                    }

                    if (!face.Contains(point, WorldTolerance(point)))
                    {
                        continue;
                    }

                    TryAddLinearPoint(
                        type, point, direction,
                        priority: SnapDefaults.OnFacePriority + 1,
                        marker: SnapMarkerKind.LineAndPoint);
                }
            }
        }

        /// <summary>
        /// Комбинирование: линейное ограничение, проходящее через точечную привязку
        /// (точки совпадают в пределах половины толеранса по экрану), становится
        /// комбинированным кандидатом с приоритетом точки + 1.
        /// </summary>
        private void PromoteCombined()
        {
            if (_anchor is null)
            {
                return;
            }

            for (int i = 0; i < _linearCandidates.Count; i++)
            {
                SnapCandidate linear = _linearCandidates[i];
                int promotedPriority = linear.Priority;
                bool promoted = false;

                foreach (SnapCandidate point in _pointCandidates)
                {
                    if (!AreCoincident(linear.Point, point.Point))
                    {
                        continue;
                    }

                    promoted = true;
                    promotedPriority = Math.Max(promotedPriority, point.Priority + 1);
                }

                if (promoted)
                {
                    _linearCandidates[i] = linear with
                    {
                        Priority = promotedPriority,
                        Marker = SnapMarkerKind.LineAndPoint,
                    };
                }
            }
        }

        private bool AreCoincident(Vector3d a, Vector3d b)
        {
            Vector2d? projectedA = _viewport.TryProject(a);
            Vector2d? projectedB = _viewport.TryProject(b);
            if (projectedA is null || projectedB is null)
            {
                return (a - b).LengthSquared() <= 0;
            }

            return Vector2d.Distance(projectedA.Value, projectedB.Value) <= _tolerancePx * 0.5;
        }

        private void TryAddPoint(SnapType type, Vector3d point)
        {
            if (!TryMeasure(point, out double screenDistance))
            {
                return;
            }

            _pointCandidates.Add(new SnapCandidate(
                type,
                point,
                Confidence(screenDistance),
                SnapDefaults.PriorityOf(type),
                SnapDefaults.MarkerOf(type))
            {
                ScreenDistancePx = screenDistance,
            });
        }

        private void TryAddLinear(
            SnapType type,
            Vector3d lineBasis,
            Vector3d direction,
            int? priority = null,
            SnapMarkerKind? marker = null)
        {
            Vector3d dir = direction.Normalized();
            if (!InferenceMath.TryClosestPointOnLineToRay(_ray, lineBasis, dir, out Vector3d point))
            {
                return;
            }

            TryAddLinearPoint(type, point, dir, priority, marker);
        }

        private void TryAddLinearPoint(
            SnapType type,
            Vector3d point,
            Vector3d direction,
            int? priority = null,
            SnapMarkerKind? marker = null)
        {
            if (!TryMeasure(point, out double screenDistance))
            {
                return;
            }

            _linearCandidates.Add(new SnapCandidate(
                type,
                point,
                Confidence(screenDistance),
                priority ?? SnapDefaults.PriorityOf(type),
                marker ?? SnapDefaults.MarkerOf(type))
            {
                Direction = direction.Normalized(),
                SecondaryPoint = _anchor,
                ScreenDistancePx = screenDistance,
            });
        }

        private void TryAddPerpendicular(Vector3d linePoint, Vector3d lineDirection, Vector3d anchor)
        {
            Vector3d foot = InferenceMath.ClosestPointOnLine(linePoint, lineDirection, anchor);
            Vector3d offset = foot - anchor;
            if (offset.LengthSquared() <= 0)
            {
                return;
            }

            if (!TryMeasure(foot, out double screenDistance))
            {
                return;
            }

            _linearCandidates.Add(new SnapCandidate(
                SnapType.Perpendicular,
                foot,
                Confidence(screenDistance),
                SnapDefaults.PriorityOf(SnapType.Perpendicular),
                SnapDefaults.MarkerOf(SnapType.Perpendicular))
            {
                Direction = offset.Normalized(),
                SecondaryPoint = anchor,
                ScreenDistancePx = screenDistance,
            });
        }

        private void TryAddTangent(Vector3d point, Vector3d anchor)
        {
            if (!TryMeasure(point, out double screenDistance))
            {
                return;
            }

            Vector3d offset = point - anchor;
            if (offset.LengthSquared() <= 0)
            {
                return;
            }

            _linearCandidates.Add(new SnapCandidate(
                SnapType.Tangent,
                point,
                Confidence(screenDistance),
                SnapDefaults.PriorityOf(SnapType.Tangent),
                SnapDefaults.MarkerOf(SnapType.Tangent))
            {
                Direction = offset.Normalized(),
                SecondaryPoint = anchor,
                ScreenDistancePx = screenDistance,
            });
        }

        /// <summary>Направления для параллельных ограничений: уникальные направления рёбер и направляющих.</summary>
        private IEnumerable<Vector3d> EnumerateReferenceDirections()
        {
            var seen = new HashSet<(long X, long Y, long Z)>();
            foreach (SnapSegment segment in _request.Scene.Segments)
            {
                Vector3d direction = segment.Direction;
                if (seen.Add(DirectionKey(direction)))
                {
                    yield return direction;
                }
            }

            foreach (SnapGuide guide in _request.Guides)
            {
                if (seen.Add(DirectionKey(guide.Direction)))
                {
                    yield return guide.Direction;
                }
            }
        }

        /// <summary>Все направления линейных ограничений (оси и параллели) для комбинирования с гранями.</summary>
        private IEnumerable<(SnapType Type, Vector3d Direction)> EnumerateConstraintDirections()
        {
            if (_allowedLinear.Contains(SnapType.AxisX))
            {
                yield return (SnapType.AxisX, Vector3d.UnitX);
            }

            if (_allowedLinear.Contains(SnapType.AxisY))
            {
                yield return (SnapType.AxisY, Vector3d.UnitY);
            }

            if (_allowedLinear.Contains(SnapType.AxisZ))
            {
                yield return (SnapType.AxisZ, Vector3d.UnitZ);
            }

            if (!_allowedLinear.Contains(SnapType.Parallel))
            {
                yield break;
            }

            foreach (Vector3d direction in EnumerateReferenceDirections())
            {
                yield return (SnapType.Parallel, direction);
            }
        }

        private bool TryMeasure(Vector3d point, out double screenDistance)
        {
            screenDistance = 0;
            Vector2d? projected = _viewport.TryProject(point);
            if (projected is null)
            {
                return false;
            }

            screenDistance = Vector2d.Distance(projected.Value, _cursor);
            return screenDistance <= _tolerancePx;
        }

        private double Confidence(double screenDistance)
            => InferenceMath.Clamp01(1.0 - screenDistance / _tolerancePx);

        /// <summary>
        /// Мировой радиус толеранса на глубине точки; 0 — точка за камерой
        /// (геометрические совпадения не засчитываются).
        /// </summary>
        private double WorldTolerance(Vector3d reference)
        {
            if (_viewport.Projection == InferenceProjection.Perspective && _viewport.DepthOf(reference) <= 0)
            {
                return 0;
            }

            return _viewport.WorldRadiusAt(reference, _tolerancePx);
        }

        private static void ResolveModifiers(
            SnapModifiers modifiers,
            out bool pointsAllowed,
            out HashSet<SnapType> allowedLinear)
        {
            allowedLinear = [];
            bool anyLock =
                (modifiers & (SnapModifiers.LockAxisX | SnapModifiers.LockAxisY | SnapModifiers.LockAxisZ
                    | SnapModifiers.LockParallel | SnapModifiers.LockPerpendicular | SnapModifiers.LockTangent)) != 0;

            if (anyLock)
            {
                // Режим заморозки направления (стрелки/Shift): только линейные ограничения.
                pointsAllowed = false;
                if ((modifiers & SnapModifiers.LockAxisX) != 0)
                {
                    allowedLinear.Add(SnapType.AxisX);
                }

                if ((modifiers & SnapModifiers.LockAxisY) != 0)
                {
                    allowedLinear.Add(SnapType.AxisY);
                }

                if ((modifiers & SnapModifiers.LockAxisZ) != 0)
                {
                    allowedLinear.Add(SnapType.AxisZ);
                }

                if ((modifiers & SnapModifiers.LockParallel) != 0)
                {
                    allowedLinear.Add(SnapType.Parallel);
                }

                if ((modifiers & SnapModifiers.LockPerpendicular) != 0)
                {
                    allowedLinear.Add(SnapType.Perpendicular);
                }

                if ((modifiers & SnapModifiers.LockTangent) != 0)
                {
                    allowedLinear.Add(SnapType.Tangent);
                }

                return;
            }

            pointsAllowed = true;
            if ((modifiers & SnapModifiers.PointsOnly) != 0)
            {
                return;
            }

            allowedLinear.Add(SnapType.AxisX);
            allowedLinear.Add(SnapType.AxisY);
            allowedLinear.Add(SnapType.AxisZ);
            allowedLinear.Add(SnapType.Parallel);
            allowedLinear.Add(SnapType.Perpendicular);
            allowedLinear.Add(SnapType.Tangent);
        }

        private static bool SharesEndpoint(SnapSegment a, SnapSegment b)
        {
            const double epsilonSquared = 1e-18;
            return (a.Start - b.Start).LengthSquared() <= epsilonSquared
                || (a.Start - b.End).LengthSquared() <= epsilonSquared
                || (a.End - b.Start).LengthSquared() <= epsilonSquared
                || (a.End - b.End).LengthSquared() <= epsilonSquared;
        }

        private static (long X, long Y, long Z) DirectionKey(Vector3d direction)
        {
            // Знак канонизируется: направление и его противоположное считаются одним.
            Vector3d d = direction.Normalized();
            if (d.X < 0 || (d.X == 0 && d.Y < 0) || (d.X == 0 && d.Y == 0 && d.Z < 0))
            {
                d = -d;
            }

            return Quantize(d);
        }

        private static List<SnapCandidate> Dedupe(List<SnapCandidate> ordered)
        {
            var seen = new HashSet<(SnapType, (long, long, long), (long, long, long), (long, long, long))>();
            var result = new List<SnapCandidate>(ordered.Count);
            foreach (SnapCandidate candidate in ordered)
            {
                var key = (
                    candidate.Type,
                    Quantize(candidate.Point),
                    Quantize(candidate.Direction ?? Vector3d.Zero),
                    Quantize(candidate.SecondaryPoint ?? Vector3d.Zero));
                if (seen.Add(key))
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private static (long X, long Y, long Z) Quantize(Vector3d v)
            => ((long)Math.Round(v.X * 1e6), (long)Math.Round(v.Y * 1e6), (long)Math.Round(v.Z * 1e6));
    }
}
