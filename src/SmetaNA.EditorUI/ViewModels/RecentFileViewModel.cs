using SmetaNA.EditorUI.Commands;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>Пункт «Недавние файлы» на стартовом экране: имя, путь и открытие.</summary>
public sealed class RecentFileViewModel
{
    public RecentFileViewModel(string fullPath, Action<string> open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(open);
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
        OpenCommand = new RelayCommand(_ => open(fullPath));
    }

    /// <summary>Полный путь к файлу проекта.</summary>
    public string FullPath { get; }

    /// <summary>Имя файла без каталога (подпись пункта).</summary>
    public string Name { get; }

    /// <summary>Команда открытия файла (параметр — путь, см. <see cref="FullPath"/>).</summary>
    public RelayCommand OpenCommand { get; }
}
