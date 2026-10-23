using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Орбитальная камера: орбита вокруг цели, панорама, зум; перспектива и ортография;
/// стандартные виды и zoom-extents по bounding box. Математика — double на
/// <see cref="Vector3d"/>/<see cref="Transform3d"/>, полностью тестируемая без UI и GL.
/// <para>
/// Система координат мира: Z вверх (соглашение SketchUp). Матрицы — в row-vector-раскладке
/// (как System.Numerics): v' = v · M. Проекция — GL-стиль, NDC z ∈ [−1, 1].
/// </para>
/// <para>
/// Параметризация орбиты: <see cref="Yaw"/> (вокруг Z, 0 — вид спереди) и
/// <see cref="Pitch"/> (±90°, полюса — виды сверху/снизу). Базис камеры аналитически
/// продолжается на полюса, поэтому «верх/низ» не вырождаются.
/// </para>
/// </summary>
public sealed class Camera
{
    /// <summary>Минимальное расстояние до цели при зуме (мм).</summary>
    public const double MinDistance = 1e-3;

    /// <summary>Максимальное расстояние до цели при зуме (мм).</summary>
    public const double MaxDistance = 1e9;

    public const double MinOrthoHeight = 1e-3;
    public const double MaxOrthoHeight = 1e12;

    private double _fieldOfViewY = Math.PI / 4;
    private double _orthoHeight = 1000;
    private double _aspectRatio = 1;
    private double _nearPlane = 1;
    private double _farPlane = 100000;
    private Vector3d _target = Vector3d.Zero;
    private double _distance = 1000;
    private double _yaw = Math.PI / 4;
    private double _pitch = 0.6154797086703874; // atan(1/√2) — истинная изометрия

    public ProjectionMode Mode { get; set; } = ProjectionMode.Perspective;

    /// <summary>Вертикальный угол зрения перспективы, радианы (0, π).</summary>
    public double FieldOfViewY
    {
        get => _fieldOfViewY;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value >= Math.PI)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Угол зрения должен быть в диапазоне (0, π).");
            }

            _fieldOfViewY = value;
        }
    }

    /// <summary>Высота видимой области ортографии в мировых единицах.</summary>
    public double OrthoHeight
    {
        get => _orthoHeight;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Высота орто-области должна быть положительной.");
            }

            _orthoHeight = value;
        }
    }

    /// <summary>Отношение ширины вьюпорта к высоте (width / height).</summary>
    public double AspectRatio
    {
        get => _aspectRatio;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Пропорция вьюпорта должна быть положительной.");
            }

            _aspectRatio = value;
        }
    }

    public double NearPlane
    {
        get => _nearPlane;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value >= _farPlane)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Ближняя плоскость должна быть в диапазоне (0, FarPlane).");
            }

            _nearPlane = value;
        }
    }

    public double FarPlane
    {
        get => _farPlane;
        set
        {
            if (!double.IsFinite(value) || value <= _nearPlane)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Дальняя плоскость должна быть больше ближней.");
            }

            _farPlane = value;
        }
    }

    /// <summary>Точка, вокруг которой вращается камера.</summary>
    public Vector3d Target
    {
        get => _target;
        set
        {
            if (!double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Z))
            {
                throw new ArgumentException("Цель камеры должна иметь конечные координаты.", nameof(value));
            }

            _target = value;
        }
    }

    /// <summary>Расстояние от камеры до цели (мм).</summary>
    public double Distance
    {
        get => _distance;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Расстояние до цели должно быть положительным.");
            }

            _distance = value;
        }
    }

    /// <summary>Поворот орбиты вокруг Z (радианы); сворачивается в (−π, π].</summary>
    public double Yaw
    {
        get => _yaw;
        set
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Угол поворота должен быть конечным числом.");
            }

            _yaw = WrapAngle(value);
        }
    }

    /// <summary>Наклон орбиты (радианы); ограничивается ±90° (полюса — виды сверху/снизу).</summary>
    public double Pitch
    {
        get => _pitch;
        set
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Угол наклона должен быть конечным числом.");
            }

            _pitch = Math.Clamp(value, -Math.PI / 2, Math.PI / 2);
        }
    }

    /// <summary>Положение камеры в мире.</summary>
    public Vector3d EyePosition => Target + EyeDirection * Distance;

    /// <summary>Единичный вектор от цели к камере.</summary>
    public Vector3d EyeDirection => new(
        Math.Sin(_yaw) * Math.Cos(_pitch),
        -Math.Cos(_yaw) * Math.Cos(_pitch),
        Math.Sin(_pitch));

    /// <summary>Направление взгляда (от камеры к цели).</summary>
    public Vector3d Forward => -EyeDirection;

    /// <summary>Правый вектор экрана (единичный).</summary>
    public Vector3d Right => new(Math.Cos(_yaw), Math.Sin(_yaw), 0);

    /// <summary>Верхний вектор экрана (единичный); на полюса продолжается аналитически.</summary>
    public Vector3d Up => new(
        -Math.Sin(_pitch) * Math.Sin(_yaw),
        Math.Sin(_pitch) * Math.Cos(_yaw),
        Math.Cos(_pitch));

    public Transform3d ViewMatrix
    {
        get
        {
            Vector3d eye = EyePosition;
            Vector3d x = Right;
            Vector3d y = Up;
            Vector3d z = EyeDirection;
            return new Transform3d(
                x.X, y.X, z.X, 0,
                x.Y, y.Y, z.Y, 0,
                x.Z, y.Z, z.Z, 0,
                -Vector3d.Dot(eye, x), -Vector3d.Dot(eye, y), -Vector3d.Dot(eye, z), 1);
        }
    }

    /// <summary>Матрица проекции: перспектива или ортография, NDC z ∈ [−1, 1].</summary>
    public Transform3d ProjectionMatrix
    {
        get
        {
            double n = _nearPlane;
            double f = _farPlane;
            if (Mode == ProjectionMode.Perspective)
            {
                double sy = 1.0 / Math.Tan(_fieldOfViewY * 0.5);
                double sx = sy / _aspectRatio;
                double a = -(f + n) / (f - n);
                double b = -2.0 * f * n / (f - n);
                return new Transform3d(
                    sx, 0, 0, 0,
                    0, sy, 0, 0,
                    0, 0, a, -1,
                    0, 0, b, 0);
            }

            double width = _orthoHeight * _aspectRatio;
            return new Transform3d(
                2.0 / width, 0, 0, 0,
                0, 2.0 / _orthoHeight, 0, 0,
                0, 0, -2.0 / (f - n), 0,
                0, 0, -(f + n) / (f - n), 1);
        }
    }

    /// <summary>Совместная матрица «мир → отсечение».</summary>
    public Transform3d ViewProjection => Transform3d.Multiply(ViewMatrix, ProjectionMatrix);

    /// <summary>
    /// Орбита вокруг цели: <paramref name="deltaYaw"/> — вокруг Z, <paramref name="deltaPitch"/> — наклон.
    /// Расстояние до цели сохраняется; наклон ограничен ±90°.
    /// </summary>
    public void Orbit(double deltaYaw, double deltaPitch)
    {
        if (!double.IsFinite(deltaYaw) || !double.IsFinite(deltaPitch))
        {
            throw new ArgumentException("Повороты орбиты должны быть конечными числами.");
        }

        Yaw = _yaw + deltaYaw;
        Pitch = _pitch + deltaPitch;
    }

    /// <summary>
    /// Панорама: сдвиг цели (и камеры вместе с ней) вдоль правого и верхнего векторов экрана.
    /// Дельты — в мировых единицах (мм); перетаскивание мышью обычно передаёт отрицательные дельты.
    /// </summary>
    public void Pan(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
        {
            throw new ArgumentException("Смещения панорамы должны быть конечными числами.");
        }

        Target += Right * deltaX + Up * deltaY;
    }

    /// <summary>
    /// Зум: <paramref name="factor"/> &gt; 1 — отдаление, &lt; 1 — приближение.
    /// Масштабирует расстояние до цели (перспектива) и высоту орто-области (орто).
    /// </summary>
    public void Zoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Масштаб должен быть положительным конечным числом.");
        }

        Distance = Math.Clamp(_distance * factor, MinDistance, MaxDistance);
        OrthoHeight = Math.Clamp(_orthoHeight * factor, MinOrthoHeight, MaxOrthoHeight);
    }

    /// <summary>Атомарная установка плоскостей отсечения (без промежуточного состояния near ≥ far).</summary>
    public void SetClipPlanes(double near, double far)
    {
        if (!double.IsFinite(near) || near <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(near), "Ближняя плоскость должна быть положительной.");
        }

        if (!double.IsFinite(far) || far <= near)
        {
            throw new ArgumentOutOfRangeException(nameof(far), "Дальняя плоскость должна быть больше ближней.");
        }

        _nearPlane = near;
        _farPlane = far;
    }

    /// <summary>
    /// Стандартный вид: задаёт ракурс (yaw/pitch), цель и расстояние сохраняются.
    /// Виды «сверху/снизу» используют полюса орбиты.
    /// </summary>
    public void SetView(StandardView view)
    {
        switch (view)
        {
            case StandardView.Front:
                _yaw = 0;
                _pitch = 0;
                break;
            case StandardView.Back:
                _yaw = Math.PI;
                _pitch = 0;
                break;
            case StandardView.Top:
                _yaw = 0;
                _pitch = Math.PI / 2;
                break;
            case StandardView.Bottom:
                _yaw = 0;
                _pitch = -Math.PI / 2;
                break;
            case StandardView.Left:
                _yaw = -Math.PI / 2;
                _pitch = 0;
                break;
            case StandardView.Right:
                _yaw = Math.PI / 2;
                _pitch = 0;
                break;
            case StandardView.Isometric:
                _yaw = Math.PI / 4;
                _pitch = Math.Atan(1.0 / Math.Sqrt(2.0));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(view), view, "Неизвестный стандартный вид.");
        }
    }

    /// <summary>
    /// Подгонка камеры под bounding box: цель — центр ящика, масштаб — по описанной сфере.
    /// Для перспективы расстояние подбирается так, что сфера целиком попадает в кадр
    /// по более узкой из сторон; для орто задаётся высота области. Плоскости отсечения
    /// выставляются с запасом вокруг ящика.
    /// </summary>
    /// <param name="bounds">Ящик модели; пустой ящик — ошибка.</param>
    /// <param name="margin">Отступ &gt; 0: 1.0 — ящик касается кадра, 1.1 — с запасом 10%.</param>
    public void ZoomExtents(BoundingBox3d bounds, double margin = 1.0)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Пустой bounding box: нечего показывать.", nameof(bounds));
        }

        if (!double.IsFinite(margin) || margin <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), "Отступ должен быть положительным конечным числом.");
        }

        double radius = bounds.DiagonalLength * 0.5;
        if (radius <= 0)
        {
            radius = 1.0; // вырожденный ящик (точка) — единичная сфера охвата
        }

        Target = bounds.Center;

        if (Mode == ProjectionMode.Perspective)
        {
            double halfY = _fieldOfViewY * 0.5;
            double halfX = Math.Atan(Math.Tan(halfY) * _aspectRatio);
            double half = Math.Min(halfY, halfX);
            Distance = Math.Clamp(margin * radius / Math.Sin(half), MinDistance, MaxDistance);
        }
        else
        {
            // Орто: масштаб — высота области, а расстояние выставляется так,
            // чтобы камера оказалась вне сферы охвата (иначе ближняя плоскость отсечёт модель).
            Distance = Math.Clamp(margin * radius * 2.0, MinDistance, MaxDistance);
            OrthoHeight = Math.Clamp(margin * radius * 2.0 * Math.Max(1.0, 1.0 / _aspectRatio), MinOrthoHeight, MaxOrthoHeight);
        }

        double near = Math.Max((Distance - radius) * 0.5, Distance * 1e-3);
        double far = (Distance + radius) * 2.0;
        SetClipPlanes(near, far);
    }

    /// <summary>
    /// Проекция мировой точки в NDC (x, y ∈ [−1, 1] — границы кадра, z ∈ [−1, 1] — плоскости отсечения).
    /// Для точек за камерой (w ≤ 0) результат не интерпретируется.
    /// </summary>
    public Vector3d Project(Vector3d worldPoint)
    {
        Transform3d m = ViewProjection;
        double x = worldPoint.X * m.M11 + worldPoint.Y * m.M21 + worldPoint.Z * m.M31 + m.M41;
        double y = worldPoint.X * m.M12 + worldPoint.Y * m.M22 + worldPoint.Z * m.M32 + m.M42;
        double z = worldPoint.X * m.M13 + worldPoint.Y * m.M23 + worldPoint.Z * m.M33 + m.M43;
        double w = worldPoint.X * m.M14 + worldPoint.Y * m.M24 + worldPoint.Z * m.M34 + m.M44;
        return new Vector3d(x / w, y / w, z / w);
    }

    private static double WrapAngle(double angle)
    {
        double wrapped = angle % (2.0 * Math.PI);
        if (wrapped > Math.PI)
        {
            wrapped -= 2.0 * Math.PI;
        }
        else if (wrapped <= -Math.PI)
        {
            wrapped += 2.0 * Math.PI;
        }

        return wrapped;
    }
}
