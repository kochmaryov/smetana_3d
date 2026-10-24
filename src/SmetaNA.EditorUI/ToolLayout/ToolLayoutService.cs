using Avalonia.Controls;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Сервис раскладки палитры — точка входа трека C для <c>MainWindow</c>
/// (координатор врезает его в волне 2): хранение раскладки, применение к
/// группам палитры без перезапуска и открытие модального диалога
/// «Настройка панели инструментов».
/// </summary>
/// <remarks>
/// Подключение в <c>MainWindowViewModel</c> (правит координатор):
/// <code>
/// _toolLayout = new ToolLayoutService(_localization, session.Shortcuts);
/// _toolLayout.LayoutChanged += RebuildPaletteGroups;
/// PaletteGroups = new ObservableCollection&lt;PaletteGroupViewModel&gt;(
///     _toolLayout.CreatePaletteGroups(ActivatePaletteItem, () => _isNoviceMode));
/// // пункт меню «Настройка панели…»:
/// await _toolLayout.OpenDialogAsync(ownerWindow);
/// </code>
/// </remarks>
public sealed class ToolLayoutService
{
    private readonly ILocalizationService _localization;
    private readonly ShortcutMap _shortcuts;

    /// <summary>Сервис поверх хранилища в каталоге настроек приложения.</summary>
    public ToolLayoutService(ILocalizationService localization, ShortcutMap shortcuts)
        : this(localization, shortcuts, new JsonToolLayoutStore())
    {
    }

    /// <summary>Сервис поверх указанного хранилища (тесты, перенос настроек).</summary>
    public ToolLayoutService(ILocalizationService localization, ShortcutMap shortcuts, IToolLayoutStore store)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(store);
        _localization = localization;
        _shortcuts = shortcuts;
        Store = store;
        Layout = store.Load();
    }

    /// <summary>Хранилище раскладки (JSON, версия схемы, миграция).</summary>
    public IToolLayoutStore Store { get; }

    /// <summary>Текущая раскладка палитры.</summary>
    public ToolLayoutModel Layout { get; private set; }

    /// <summary>Раскладка изменилась (диалог применён, сброс или перечитывание).</summary>
    public event Action? LayoutChanged;

    /// <summary>Перечитать раскладку из хранилища.</summary>
    public void Reload()
    {
        Layout = Store.Load();
        LayoutChanged?.Invoke();
    }

    /// <summary>Сохранить раскладку и применить её (без перезапуска).</summary>
    public void Save(ToolLayoutModel layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Store.Save(layout);
        Layout = layout;
        LayoutChanged?.Invoke();
    }

    /// <summary>Сбросить раскладку к дефолтной и применить её.</summary>
    public ToolLayoutModel Reset()
    {
        Layout = Store.Reset();
        LayoutChanged?.Invoke();
        return Layout;
    }

    /// <summary>
    /// Группы палитры по раскладке: видимость, порядок и свои группы; под-инструменты
    /// вылетов остаются при своей кнопке.
    /// </summary>
    public IReadOnlyList<PaletteGroup> ResolvePaletteGroups(ToolLayoutModel? layout = null) =>
        ToolLayoutPaletteResolver.Resolve(layout ?? Layout);

    /// <summary>
    /// View-модели групп палитры для <c>MainWindowViewModel.PaletteGroups</c>:
    /// группы + кнопки-заглушки (вылеты подключает трек B).
    /// </summary>
    public IReadOnlyList<PaletteGroupViewModel> CreatePaletteGroups(
        Action<ToolStubViewModel> activate,
        Func<bool>? showHints = null,
        ToolLayoutModel? layout = null)
    {
        ArgumentNullException.ThrowIfNull(activate);
        List<PaletteGroupViewModel> result = [];
        foreach (PaletteGroup group in ResolvePaletteGroups(layout))
        {
            List<ToolStubViewModel> buttons =
            [
                .. group.Buttons.Select(button =>
                    new ToolStubViewModel(_localization, _shortcuts, button.Definition, activate, showHints)),
            ];
            result.Add(new PaletteGroupViewModel(_localization, group, buttons));
        }

        return result;
    }

    /// <summary>
    /// Открыть модальный диалог «Настройка панели инструментов» (макет variant_a).
    /// <c>true</c> — пользователь применил раскладку; она сохранена и
    /// <see cref="LayoutChanged"/> уже сработал.
    /// </summary>
    public async Task<bool> OpenDialogAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ToolLayoutDialogViewModel viewModel = new(_localization, _shortcuts, Layout);
        ToolLayoutDialogWindow window = new(viewModel);
        bool applied = await window.ShowDialog<bool>(owner);
        if (applied)
        {
            Save(viewModel.BuildLayout());
        }

        return applied;
    }
}
