using System.Collections.ObjectModel;
using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Icons;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ToolPalette;
using SmetaNA.EditorUI.Vcb;
using SmetaNA.EditorUI.ViewModels;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Кнопка в диалоге настройки: одна запись и для дерева «Все инструменты», и для
/// «Панели» (инструмент живёт в одной группе — см. <see cref="Group"/>).
/// Под-инструменты вылета — дочерние узлы без собственной видимости: они следуют
/// за своей кнопкой.
/// </summary>
public sealed class ToolLayoutToolNode : ViewModelBase
{
    private readonly ShortcutMap _shortcuts;
    private readonly ToolLayoutToolNode? _flyoutOwner;
    private readonly int _flyoutMenuCount;
    private readonly ILocalizationService Localization;
    private bool _isVisible = true;
    private bool _isSelected;
    private bool _isMatch = true;
    private bool _showHint = true;
    private bool _showGesture = true;
    private bool _hideCompact;

    public ToolLayoutToolNode(
        ILocalizationService localization,
        ShortcutMap shortcuts,
        PaletteButton button,
        string defaultGroupId,
        ToolLayoutToolNode? flyoutOwner = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(button);
        ArgumentException.ThrowIfNullOrEmpty(defaultGroupId);
        Localization = localization;
        _shortcuts = shortcuts;
        _flyoutOwner = flyoutOwner;
        _flyoutMenuCount = flyoutOwner is null ? button.FlyoutItems.Count : 0;
        DefaultGroupId = defaultGroupId;
        CommandId = button.Definition.CommandId;
        NameKey = button.Definition.NameKey;
        VcbKind = button.Definition.VcbKind;
        IconId = button.IconId;
        FlyoutItems =
        [
            .. button.FlyoutItems
                .Where(item => item.Definition.CommandId != CommandId)
                .Select(item => new ToolLayoutToolNode(localization, shortcuts, item, defaultGroupId, this)),
        ];
    }

    /// <summary>Команда кнопки (совпадает с <c>ToolDefinition.CommandId</c>).</summary>
    public string CommandId { get; }

    /// <summary>Ключ имени в общем каталоге локализации.</summary>
    public string NameKey { get; }

    /// <summary>Вид числового ввода в VCB (или <c>null</c>).</summary>
    public VcbInputKind? VcbKind { get; }

    /// <summary>Глиф кнопки (контракт иконок волны 0).</summary>
    public ToolIconId IconId { get; }

    /// <summary>Под-инструменты вылета («Дуга», «Зеркало»); пусто — вылета нет.</summary>
    public IReadOnlyList<ToolLayoutToolNode> FlyoutItems { get; }

    /// <summary>Кнопка-владелец для под-инструмента; <c>null</c> — кнопка верхнего уровня.</summary>
    public ToolLayoutToolNode? FlyoutOwner => _flyoutOwner;

    /// <summary>Группа реестра, куда инструмент попадает по умолчанию.</summary>
    public string DefaultGroupId { get; }

    /// <summary>Текущая группа на «Панели»; <c>null</c> — кнопка на панель не поставлена.</summary>
    public ToolLayoutPanelGroupNode? Group { get; internal set; }

    /// <summary>Локализованное имя кнопки.</summary>
    public string Name => Localization.Get(NameKey);

    /// <summary>Текущая горячая клавиша из <see cref="ShortcutMap"/>; пусто — не назначена.</summary>
    public string Gesture => _shortcuts.Find(CommandId)?.ToString() ?? string.Empty;

    /// <summary>Число под-инструментов в вылете (0 — вылета нет).</summary>
    public int FlyoutCount => _flyoutMenuCount;

    /// <summary>Подпись строки панели: жест или «N в вылете».</summary>
    public string Badge
    {
        get
        {
            if (HasFlyout)
            {
                return ToolLayoutDialogStrings.FormatFlyoutCount(FlyoutCount, Localization.Culture);
            }

            return string.IsNullOrEmpty(Gesture)
                ? ToolLayoutDialogStrings.Resolve(Localization, ToolLayoutDialogStrings.DialogPropertyNone)
                : Gesture;
        }
    }

    /// <summary>Кнопка раскрывается вылетом («Дуга», «Зеркало»).</summary>
    public bool HasFlyout => _flyoutOwner is null && FlyoutItems.Count > 0;

    /// <summary>Кнопка видима на палитре; под-инструменты вылета следуют за кнопкой.</summary>
    public bool IsVisible
    {
        get => _flyoutOwner?.IsVisible ?? _isVisible;
        set
        {
            if (_flyoutOwner is not null)
            {
                _flyoutOwner.IsVisible = value;
                return;
            }

            if (SetField(ref _isVisible, value))
            {
                Group?.RefreshCounts();
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    /// <summary>Строка выбрана в диалоге (колонка «Свойства кнопки»).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Строка проходит фильтр поиска дерева «Все инструменты».</summary>
    public bool IsMatch
    {
        get => _isMatch;
        set => SetField(ref _isMatch, value);
    }

    /// <summary>Подсказка новичку в тултипе (состояние сессии; schema v1 не хранит).</summary>
    public bool ShowHint
    {
        get => _showHint;
        set => SetField(ref _showHint, value);
    }

    /// <summary>Горячая клавиша в тултипе (состояние сессии; schema v1 не хранит).</summary>
    public bool ShowGesture
    {
        get => _showGesture;
        set => SetField(ref _showGesture, value);
    }

    /// <summary>Скрывать кнопку в компактном режиме (состояние сессии; schema v1 не хранит).</summary>
    public bool HideCompact
    {
        get => _hideCompact;
        set => SetField(ref _hideCompact, value);
    }

    /// <summary>Имя группы кнопки для «Свойств кнопки».</summary>
    public string GroupName => Group?.DisplayText ?? string.Empty;

    /// <summary>Маркер перетаскивания «⠿» (макет variant_a).</summary>
    public string GripText => ToolLayoutDialogStrings.Resolve(Localization, ToolLayoutDialogStrings.DialogGrip);

    /// <summary>Локализованное имя вида ввода VCB; «—» если ввода нет.</summary>
    public string VcbText => VcbKind is null
        ? ToolLayoutDialogStrings.Resolve(Localization, ToolLayoutDialogStrings.DialogPropertyNone)
        : Localization.Get(LocalizationKeys.ForVcbPrompt(VcbKind.Value));

    /// <summary>Искать по имени или горячей клавише (фильтр дерева).</summary>
    public bool Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
            || Gesture.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
            || CommandId.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Пересчитать локализованные подписи (смена языка).</summary>
    public void RefreshText() => InvalidateAll();
}

/// <summary>Группа в диалоге настройки: раздел дерева или «Панели».</summary>
public abstract class ToolLayoutGroupNodeBase : ViewModelBase
{
    private bool _isSelected;
    private bool _isMatch = true;

    protected ToolLayoutGroupNodeBase(ILocalizationService localization, string id, string? nameKey, string? customName)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentException.ThrowIfNullOrEmpty(id);
        Localization = localization;
        Id = id;
        NameKey = nameKey;
        CustomName = customName;
    }

    /// <summary>Сервис локализации для имён групп и подписей.</summary>
    protected ILocalizationService Localization { get; }

    public string Id { get; }

    /// <summary>Ключ имени системной группы; <c>null</c> для пользовательской.</summary>
    public string? NameKey { get; }

    /// <summary>Имя пользовательской группы; <c>null</c> — имя из реестра.</summary>
    public string? CustomName { get; protected set; }

    /// <summary>Пользовательская группа (создаётся, переименовывается и удаляется).</summary>
    public bool IsCustom => CustomName is not null;

    /// <summary>Кнопки группы (на «Панели» — включая скрытые).</summary>
    public ObservableCollection<ToolLayoutToolNode> Items { get; } = [];

    /// <summary>Локализованное имя группы.</summary>
    public string DisplayText => CustomName ?? (NameKey is null ? Id : Localization.Get(NameKey));

    /// <summary>Строка выбрана в диалоге.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Строка проходит фильтр поиска дерева «Все инструменты».</summary>
    public bool IsMatch
    {
        get => _isMatch;
        set => SetField(ref _isMatch, value);
    }

    /// <summary>Пересчитать локализованные подписи (смена языка).</summary>
    public void RefreshText()
    {
        InvalidateAll();
        foreach (ToolLayoutToolNode item in Items)
        {
            item.RefreshText();
        }
    }
}

/// <summary>Группа дерева «Все инструменты»: состав реестра, чекбоксы — у кнопок.</summary>
public sealed class ToolLayoutAllGroupNode : ToolLayoutGroupNodeBase
{
    public ToolLayoutAllGroupNode(ILocalizationService localization, PaletteGroup group)
        : base(localization, group.Id, group.NameKey, customName: null)
    {
    }

    /// <summary>Сколько кнопок в группе реестра («Рисование · 7»).</summary>
    public string CountText => ToolLayoutDialogStrings.FormatToolCount(Items.Count, Localization.Culture);
}

/// <summary>Группа «Панели (порядок и группы)»: порядок, видимость, свои группы.</summary>
public sealed class ToolLayoutPanelGroupNode : ToolLayoutGroupNodeBase
{
    private bool _isVisible = true;

    public ToolLayoutPanelGroupNode(ILocalizationService localization, string id, string? nameKey, string? customName)
        : base(localization, id, nameKey, customName)
    {
    }

    /// <summary>Группа видима на палитре (для системных — единственная настройка).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetField(ref _isVisible, value))
            {
                RefreshCounts();
            }
        }
    }

    /// <summary>Системную группу можно только скрыть (спека §7).</summary>
    public bool CanRename => IsCustom;

    /// <summary>Системную группу можно только скрыть (спека §7).</summary>
    public bool CanDelete => IsCustom;

    /// <summary>Сколько видимых кнопок на панели («5 кнопок»).</summary>
    public string CountText => ToolLayoutDialogStrings.FormatToolCount(
        Items.Count(item => item.IsVisible),
        Localization.Culture);

    /// <summary>Маркер перетаскивания «⠿» (макет variant_a).</summary>
    public string GripText => ToolLayoutDialogStrings.Resolve(Localization, ToolLayoutDialogStrings.DialogGrip);

    /// <summary>Подсказка чекбокса видимости группы.</summary>
    public string GroupVisibleToolTip =>
        ToolLayoutDialogStrings.Resolve(Localization, ToolLayoutDialogStrings.DialogGroupVisible);

    /// <summary>Группа скрыта — строка показана приглушённой.</summary>
    public bool IsDimmed => !IsVisible;

    /// <summary>Пересчитать счётчики кнопок (смена состава или видимости).</summary>
    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(IsDimmed));
    }

    /// <summary>Задать имя пользовательской группы (для системных — без изменений).</summary>
    internal void Rename(string? customName)
    {
        if (!IsCustom)
        {
            return;
        }

        CustomName = customName;
        RefreshText();
    }
}
