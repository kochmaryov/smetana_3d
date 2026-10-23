using SmetaNA.Core.Geometry;

namespace SmetaNA.Core.Inference;

/// <summary>Режим проекции вьюпорта для inference-расчётов.</summary>
public enum InferenceProjection
{
    Perspective,
    Orthographic,
}

/// <summary>
/// Параметры камеры и вьюпорта, необходимые inference-движку: проекция мировых
/// точек в пиксели, построение луча выбора по курсору и перевод толеранса
/// из пикселей экрана в мировые единицы.
/// <para>
/// Мир — Z вверх (соглашение SketchUp). Камера смотрит вдоль единичного
/// <see cref="Forward"/>; <see cref="Right"/> = normalize(Forward × Up).
/// Экранные координаты — <see cref="Vector2d"/>: начало в левом верхнем углу,
/// X вправо, Y вниз. <see cref="InferenceProjection.Perspective"/>: глубина точки —
/// проекция на <see cref="Forward"/>, точки за камерой не проецируются.
/// </para>
/// </summary>
public sealed class InferenceViewport
{
    private readonly double _tanHalfFovY;

    private InferenceViewport(
        InferenceProjection projection,
        Vector3d eyePosition,
        Vector3d forward,
        Vector3d up,
        Vector3d right,
        double fieldOfViewY,
        double orthoHeight,
        double widthPx,
        double heightPx)
    {
        Projection = projection;
        EyePosition = eyePosition;
        Forward = forward;
        Up = up;
        Right = right;
        FieldOfViewY = fieldOfViewY;
        OrthoHeight = orthoHeight;
        WidthPx = widthPx;
        HeightPx = heightPx;
        _tanHalfFovY = Math.Tan(fieldOfViewY * 0.5);
    }

    public InferenceProjection Projection { get; }

    public Vector3d EyePosition { get; }

    /// <summary>Направление взгляда, единичное.</summary>
    public Vector3d Forward { get; }

    /// <summary>Верх вьюпорта, единичный; ортогонален <see cref="Forward"/>.</summary>
    public Vector3d Up { get; }

    /// <summary>Право вьюпорта, единичный.</summary>
    public Vector3d Right { get; }

    /// <summary>Вертикальный угол зрения, радианы; используется только в перспективе.</summary>
    public double FieldOfViewY { get; }

    /// <summary>Высота видимой области в мировых единицах; используется только в ортографии.</summary>
    public double OrthoHeight { get; }

    public double WidthPx { get; }

    public double HeightPx { get; }

    /// <summary>Перспективный вьюпорт.</summary>
    public static InferenceViewport CreatePerspective(
        Vector3d eyePosition,
        Vector3d forward,
        Vector3d up,
        double fieldOfViewY,
        double widthPx,
        double heightPx)
    {
        ValidateFieldOfView(fieldOfViewY);
        return Create(
            InferenceProjection.Perspective, eyePosition, forward, up, fieldOfViewY, 0, widthPx, heightPx);
    }

    /// <summary>Ортографический вьюпорт.</summary>
    public static InferenceViewport CreateOrthographic(
        Vector3d eyePosition,
        Vector3d forward,
        Vector3d up,
        double orthoHeight,
        double widthPx,
        double heightPx)
    {
        if (!double.IsFinite(orthoHeight) || orthoHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orthoHeight), "Высота орто-области должна быть положительной.");
        }

        return Create(
            InferenceProjection.Orthographic, eyePosition, forward, up, 0, orthoHeight, widthPx, heightPx);
    }

    /// <summary>Глубина точки в системе камеры; отрицательное значение — за камерой.</summary>
    public double DepthOf(Vector3d point) => Vector3d.Dot(point - EyePosition, Forward);

    /// <summary>
    /// Проекция мировой точки в пиксели; <c>null</c> — точка за камерой
    /// (только перспектива).
    /// </summary>
    public Vector2d? TryProject(Vector3d point)
    {
        Vector3d offset = point - EyePosition;
        double depth = Vector3d.Dot(offset, Forward);
        if (Projection == InferenceProjection.Perspective && depth <= 0)
        {
            return null;
        }

        double x = Vector3d.Dot(offset, Right);
        double y = Vector3d.Dot(offset, Up);
        double aspect = WidthPx / HeightPx;

        double ndcX;
        double ndcY;
        if (Projection == InferenceProjection.Perspective)
        {
            ndcX = x / (depth * _tanHalfFovY * aspect);
            ndcY = y / (depth * _tanHalfFovY);
        }
        else
        {
            ndcX = x / (OrthoHeight * aspect * 0.5);
            ndcY = y / (OrthoHeight * 0.5);
        }

        return new Vector2d((ndcX + 1.0) * 0.5 * WidthPx, (1.0 - ndcY) * 0.5 * HeightPx);
    }

    /// <summary>Луч выбора, идущий из камеры через пиксель курсора.</summary>
    public Ray3d PickRay(Vector2d pixel)
    {
        double aspect = WidthPx / HeightPx;
        double ndcX = pixel.X / WidthPx * 2.0 - 1.0;
        double ndcY = 1.0 - pixel.Y / HeightPx * 2.0;

        if (Projection == InferenceProjection.Perspective)
        {
            Vector3d direction = Forward
                + Right * (ndcX * _tanHalfFovY * aspect)
                + Up * (ndcY * _tanHalfFovY);
            return new Ray3d(EyePosition, direction);
        }

        Vector3d origin = EyePosition
            + Right * (ndcX * OrthoHeight * aspect * 0.5)
            + Up * (ndcY * OrthoHeight * 0.5);
        return new Ray3d(origin, Forward);
    }

    /// <summary>
    /// Мировой радиус, соответствующий радиусу <paramref name="pixelRadius"/> пикселей
    /// на глубине точки <paramref name="point"/>. Для перспективы точка должна быть перед камерой.
    /// </summary>
    public double WorldRadiusAt(Vector3d point, double pixelRadius)
    {
        if (!double.IsFinite(pixelRadius) || pixelRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelRadius), "Радиус в пикселях не может быть отрицательным.");
        }

        if (Projection == InferenceProjection.Orthographic)
        {
            return pixelRadius * OrthoHeight / HeightPx;
        }

        double depth = DepthOf(point);
        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "Точка за камерой не имеет мирового масштаба пикселя.");
        }

        return pixelRadius * 2.0 * depth * _tanHalfFovY / HeightPx;
    }

    private static InferenceViewport Create(
        InferenceProjection projection,
        Vector3d eyePosition,
        Vector3d forward,
        Vector3d up,
        double fieldOfViewY,
        double orthoHeight,
        double widthPx,
        double heightPx)
    {
        if (!InferenceMath.IsFinite(eyePosition))
        {
            throw new ArgumentException("Положение камеры должно быть конечным.", nameof(eyePosition));
        }

        if (!InferenceMath.IsFinite(forward) || forward.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление взгляда должно быть ненулевым.", nameof(forward));
        }

        if (!InferenceMath.IsFinite(up) || up.LengthSquared() <= 0)
        {
            throw new ArgumentException("Вертикаль вьюпорта должна быть ненулевой.", nameof(up));
        }

        if (!double.IsFinite(widthPx) || widthPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), "Ширина вьюпорта должна быть положительной.");
        }

        if (!double.IsFinite(heightPx) || heightPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightPx), "Высота вьюпорта должна быть положительной.");
        }

        Vector3d f = forward.Normalized();
        Vector3d right = Vector3d.Cross(f, up.Normalized());
        if (right.LengthSquared() <= 0)
        {
            throw new ArgumentException("Направление взгляда и вертикаль не должны быть коллинеарны.", nameof(up));
        }

        right = right.Normalized();
        // Up пересобирается строго ортогональным к Forward.
        Vector3d correctedUp = Vector3d.Cross(right, f).Normalized();
        return new InferenceViewport(
            projection, eyePosition, f, correctedUp, right, fieldOfViewY, orthoHeight, widthPx, heightPx);
    }

    private static void ValidateFieldOfView(double fieldOfViewY)
    {
        if (!double.IsFinite(fieldOfViewY) || fieldOfViewY <= 0 || fieldOfViewY >= Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewY), "Угол зрения должен быть в диапазоне (0, π).");
        }
    }
}
