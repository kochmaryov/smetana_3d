using SmetaNA.Core.Geometry;

namespace SmetaNA.Rendering;

/// <summary>
/// Построение луча из камеры через точку экрана (пиксели вьюпорта, начало — левый верх).
/// Обратная операция к <see cref="Camera.Project"/>; работает для перспективы и орто.
/// </summary>
public static class CameraRay
{
    public static Ray3d FromViewportPoint(
        Camera camera,
        double pixelX,
        double pixelY,
        double viewportWidth,
        double viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Размеры вьюпорта должны быть положительными.");
        }

        double ndcX = (2.0 * pixelX / viewportWidth) - 1.0;
        double ndcY = 1.0 - (2.0 * pixelY / viewportHeight);

        Vector3d eye = camera.EyePosition;
        if (camera.Mode == ProjectionMode.Perspective)
        {
            double tanHalfY = Math.Tan(camera.FieldOfViewY * 0.5);
            double tanHalfX = tanHalfY * camera.AspectRatio;
            Vector3d direction = (
                camera.Forward +
                (camera.Right * (ndcX * tanHalfX)) +
                (camera.Up * (ndcY * tanHalfY))).Normalized();
            return new Ray3d(eye, direction);
        }

        double halfHeight = camera.OrthoHeight * 0.5;
        double halfWidth = halfHeight * camera.AspectRatio;
        Vector3d origin = eye + (camera.Right * (ndcX * halfWidth)) + (camera.Up * (ndcY * halfHeight));
        return new Ray3d(origin, camera.Forward);
    }
}
