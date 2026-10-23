using SmetaNA.Rendering.PrintVolume;

namespace SmetaNA.Rendering;

/// <summary>
/// Подключение зоны печати (задача 4.5) к GL-вьюпорту: оверлей строится лениво
/// из текущего профиля <see cref="PrintVolumeState"/> и модели сцены и рисуется
/// после кадра сцены. Зона выключена (профиль <c>null</c>) — накладных расходов нет.
/// </summary>
public sealed partial class GlViewportControl
{
    private PrintVolumeGlRenderer? _printVolumeRenderer;
    private PrinterProfile? _printVolumeProfile;
    private SceneRenderModel? _printVolumeModelRef;
    private PrintVolumeOverlay? _printVolumeOverlay;

    /// <summary>Построенный оверлей зоны печати или <c>null</c> (зона выключена).</summary>
    internal PrintVolumeOverlay? PrintVolumeOverlayState => _printVolumeOverlay;

    private void DrawPrintVolume(float[] mvp, float[] mv)
    {
        PrinterProfile? profile = PrintVolumeState.Current;
        if (profile is null)
        {
            _printVolumeOverlay = null;
            _printVolumeProfile = null;
            _printVolumeModelRef = null;
            return;
        }

        if (!ReferenceEquals(profile, _printVolumeProfile) || !ReferenceEquals(_model, _printVolumeModelRef))
        {
            _printVolumeOverlay = PrintVolumeOverlay.Build(profile, _model);
            _printVolumeProfile = profile;
            _printVolumeModelRef = _model;
        }

        if (_gl is not { } gl)
        {
            return;
        }

        _printVolumeRenderer ??= new PrintVolumeGlRenderer();
        _printVolumeRenderer.Draw(gl, _printVolumeOverlay, mvp, mv);
    }

    private void DisposePrintVolume()
    {
        _printVolumeRenderer?.Dispose();
        _printVolumeRenderer = null;
        _printVolumeOverlay = null;
        _printVolumeProfile = null;
        _printVolumeModelRef = null;
    }
}
