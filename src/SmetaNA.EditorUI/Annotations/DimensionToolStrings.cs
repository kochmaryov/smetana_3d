namespace SmetaNA.EditorUI.Annotations;

/// <summary>
/// Строки модуля размеров (M4): свой файл локализации модуля, чтобы не трогать
/// общие ресурсы <c>Localization</c> до подключения координатором после merge.
/// </summary>
public sealed record DimensionToolStrings(
    string DimensionToolName,
    string ScaleToDimensionToolName,
    string DimensionHint,
    string ScaleToDimensionHint,
    string CreateDimensionUndo,
    string ScaleModelUndo)
{
    /// <summary>Русские строки (по умолчанию).</summary>
    public static DimensionToolStrings Ru { get; } = new(
        DimensionToolName: "Размер",
        ScaleToDimensionToolName: "Масштаб по размеру",
        DimensionHint: "Клик — точки/ребро, второй клик — конец, третий — линия размера; VCB — точная длина/смещение",
        ScaleToDimensionHint: "Отметьте отрезок и введите целевой размер — модель масштабируется с сохранением пропорций",
        CreateDimensionUndo: "Добавление размера",
        ScaleModelUndo: "Масштабирование по размеру");

    /// <summary>Английские строки.</summary>
    public static DimensionToolStrings En { get; } = new(
        DimensionToolName: "Dimension",
        ScaleToDimensionToolName: "Scale to dimension",
        DimensionHint: "Click — points/edge, second click — end, third — dimension line; VCB — exact length/offset",
        ScaleToDimensionHint: "Pick the segment and type the target size — the model scales proportionally",
        CreateDimensionUndo: "Add dimension",
        ScaleModelUndo: "Scale to dimension");

    /// <summary>Строки по имени культуры («en*» — английские, иначе русские).</summary>
    public static DimensionToolStrings For(string? cultureName)
        => cultureName is not null && cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? En : Ru;
}
