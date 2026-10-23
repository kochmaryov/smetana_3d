using SmetaNA.Core.Geometry;

namespace SmetaNA.Formats;

/// <summary>Результат импорта: нормализованный меш (мм) и отчёт о потерях/дефектах.</summary>
public sealed record MeshImportResult(MeshData Mesh, MeshImportReport Report);
