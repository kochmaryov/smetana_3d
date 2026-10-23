using SmetaNA.Core.Annotations;
using SmetaNA.Core.Inference;
using SmetaNA.Core.Model;
using SmetaNA.Core.Tools;
using SmetaNA.Rendering;
using SmetaNA.Rendering.Annotations;

namespace SmetaNA.EditorUI.Annotations;

/// <summary>Описание инструмента размеров для панели инструментов/команд UI.</summary>
/// <param name="Id">Идентификатор команды (совпадает с <see cref="ITool.Id"/>).</param>
/// <param name="DisplayName">Локализованное имя.</param>
/// <param name="Create">Фабрика экземпляра инструмента.</param>
public sealed record DimensionToolDescriptor(string Id, string DisplayName, Func<ITool> Create);

/// <summary>
/// Модуль интеграции M4 (размеры и аннотации): фабрика инструментов и строк,
/// построение батча размеров для GL-оверлея вьюпорта. Существующие файлы EditorUI
/// не меняются — координатор подключает модуль после merge
/// (вызовы <see cref="CreateDescriptors"/> и <see cref="RefreshAnnotations"/>).
/// </summary>
public static class RegisterDimensionTools
{
    /// <summary>Идентификатор команды «Размер».</summary>
    public const string DimensionCommandId = DimensionTool.ToolId;

    /// <summary>Идентификатор команды «Масштаб по размеру».</summary>
    public const string ScaleToDimensionCommandId = ScaleToDimensionTool.ToolId;

    /// <summary>Описания инструментов размеров (в порядке панели).</summary>
    public static IReadOnlyList<DimensionToolDescriptor> CreateDescriptors(DimensionToolStrings? strings = null)
    {
        DimensionToolStrings s = strings ?? DimensionToolStrings.Ru;
        return
        [
            new DimensionToolDescriptor(DimensionCommandId, s.DimensionToolName, () => new DimensionTool()),
            new DimensionToolDescriptor(ScaleToDimensionCommandId, s.ScaleToDimensionToolName, () => new ScaleToDimensionTool()),
        ];
    }

    /// <summary>Батч размеров/меток сцены для оверлея вьюпорта.</summary>
    public static DimensionBatch BuildAnnotationBatch(
        Scene scene,
        InferenceViewport viewport,
        DimensionStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(viewport);
        return DimensionRenderer.Build(AnnotationQuery.FindInScene(scene), viewport, scene.Unit, style);
    }

    /// <summary>
    /// Обновить батч размеров вьюпорта из сцены (вызывать после правок сцены/камеры;
    /// кадр вьюпорта перерисуется на следующем тике рендера).
    /// </summary>
    public static void RefreshAnnotations(
        GlViewportControl viewport,
        Scene scene,
        double widthPx,
        double heightPx,
        DimensionStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(scene);
        viewport.AnnotationBatch = BuildAnnotationBatch(
            scene,
            ToInferenceViewport(viewport.Camera, widthPx, heightPx),
            style);
    }

    /// <summary>Мост камеры GL-вьюпорта во вьюпорт привязок/проекций.</summary>
    public static InferenceViewport ToInferenceViewport(Camera camera, double widthPx, double heightPx)
    {
        ArgumentNullException.ThrowIfNull(camera);
        double width = Math.Max(1.0, widthPx);
        double height = Math.Max(1.0, heightPx);
        return camera.Mode == ProjectionMode.Perspective
            ? InferenceViewport.CreatePerspective(
                camera.EyePosition, camera.Forward, camera.Up, camera.FieldOfViewY, width, height)
            : InferenceViewport.CreateOrthographic(
                camera.EyePosition, camera.Forward, camera.Up, camera.OrthoHeight, width, height);
    }
}
