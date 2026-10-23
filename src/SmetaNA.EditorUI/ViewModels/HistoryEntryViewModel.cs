namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Запись панели истории с меткой текущего состояния.</summary>
/// <param name="Description">Описание команды.</param>
/// <param name="CurrentLabel">Локализованная метка текущего состояния.</param>
/// <param name="IsCurrent">Запись соответствует текущему состоянию.</param>
public sealed record HistoryEntryViewModel(string Description, string CurrentLabel, bool IsCurrent);
