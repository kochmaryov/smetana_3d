using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SmetaNA.EditorUI.ToolLayout;

/// <summary>
/// Модальный диалог «Настройка панели инструментов» (макет variant_a). Логика —
/// <see cref="ToolLayoutDialogViewModel"/>; в code-behind — только выбор строк,
/// drag&amp;drop порядка за «⠿» и запросы имён своих групп.
/// </summary>
public partial class ToolLayoutDialogWindow : Window
{
    private const string RowTag = "panelRow";
    private const double DragThreshold = 4;

    private Control? _dragRow;
    private ToolLayoutToolNode? _dragTool;
    private ToolLayoutPanelGroupNode? _dragGroup;
    private Point _pressPoint;
    private bool _dragging;

    public ToolLayoutDialogWindow()
        : this(new ToolLayoutDialogViewModel(
            EditorServices.Localization,
            new Commands.ShortcutMap(),
            ToolLayoutModel.CreateDefault()))
    {
    }

    public ToolLayoutDialogWindow(ToolLayoutDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += ok => Close(ok);
    }

    internal ToolLayoutDialogViewModel ViewModel { get; }

    private void OnToolRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ToolLayoutToolNode tool })
        {
            ViewModel.SelectTool(tool);
        }
    }

    private void OnGroupRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ToolLayoutPanelGroupNode group })
        {
            ViewModel.SelectGroup(group);
        }
    }

    private void OnNewGroupClick(object? sender, RoutedEventArgs e) =>
        _ = PromptGroupNameAsync(isNewGroup: true);

    private void OnRenameGroupClick(object? sender, RoutedEventArgs e) =>
        _ = PromptGroupNameAsync(isNewGroup: false);

    private async Task PromptGroupNameAsync(bool isNewGroup)
    {
        ToolLayoutPanelGroupNode? renaming = ViewModel.SelectedGroup;
        if (!isNewGroup && renaming is not { CanRename: true })
        {
            return;
        }

        ToolLayoutGroupNameViewModel prompt = isNewGroup
            ? ToolLayoutGroupNameViewModel.ForNewGroup(EditorServices.Localization, ViewModel.DefaultGroupName)
            : ToolLayoutGroupNameViewModel.ForRename(
                EditorServices.Localization,
                renaming!.CustomName ?? string.Empty);
        ToolLayoutGroupNameWindow window = new(prompt);
        bool confirmed = await window.ShowDialog<bool>(this);
        if (!confirmed)
        {
            return;
        }

        if (isNewGroup)
        {
            ViewModel.CreateGroup(prompt.Name);
        }
        else
        {
            ViewModel.RenameGroup(renaming!, prompt.Name);
        }
    }

    // --- Drag&drop порядка на «Панели»: строки групп и кнопок ---

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Control? row = FindRow(e.Source as Visual, RowTag);
        if (row is null || FindAncestor<CheckBox>(e.Source as Visual) is not null)
        {
            return;
        }

        switch (row.DataContext)
        {
            case ToolLayoutToolNode tool:
                ViewModel.SelectTool(tool);
                _dragTool = tool;
                _dragGroup = null;
                break;
            case ToolLayoutPanelGroupNode group:
                ViewModel.SelectGroup(group);
                _dragGroup = group;
                _dragTool = null;
                break;
            default:
                return;
        }

        _dragRow = row;
        _pressPoint = e.GetPosition(this);
        _dragging = false;
        e.Pointer.Capture(PanelList);
    }

    private void OnPanelPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragRow is null || _dragging)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (Math.Abs(position.X - _pressPoint.X) > DragThreshold
            || Math.Abs(position.Y - _pressPoint.Y) > DragThreshold)
        {
            _dragging = true;
            _dragRow.Opacity = 0.6;
        }
    }

    private void OnPanelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Control? row = _dragRow;
        bool wasDragging = _dragging;
        ToolLayoutToolNode? tool = _dragTool;
        ToolLayoutPanelGroupNode? group = _dragGroup;
        _dragRow = null;
        _dragTool = null;
        _dragGroup = null;
        _dragging = false;

        if (row is null)
        {
            return;
        }

        row.Opacity = 1;
        e.Pointer.Capture(null);
        if (!wasDragging)
        {
            return;
        }

        Control? target = FindRowAt(e.GetPosition(this));
        if (target is null)
        {
            return;
        }

        if (tool is not null)
        {
            DropTool(tool, target, e.GetPosition(target));
        }
        else if (group is not null)
        {
            DropGroup(group, target, e.GetPosition(target));
        }
    }

    private void DropTool(ToolLayoutToolNode tool, Control targetRow, Point positionInTarget)
    {
        switch (targetRow.DataContext)
        {
            case ToolLayoutToolNode target when target.Group is not null && !ReferenceEquals(target, tool):
            {
                ToolLayoutPanelGroupNode targetGroup = target.Group;
                int visibleIndex = targetGroup.Items
                    .TakeWhile(item => !ReferenceEquals(item, target))
                    .Count(item => item.IsVisible);
                if (positionInTarget.Y > targetRow.Bounds.Height / 2)
                {
                    visibleIndex++;
                }

                ViewModel.MoveToolTo(tool, targetGroup, visibleIndex);
                break;
            }

            case ToolLayoutPanelGroupNode targetGroup:
                ViewModel.MoveToolTo(tool, targetGroup, targetGroup.Items.Count(item => item.IsVisible));
                break;
        }
    }

    private void DropGroup(ToolLayoutPanelGroupNode group, Control targetRow, Point positionInTarget)
    {
        ToolLayoutPanelGroupNode? targetGroup = targetRow.DataContext switch
        {
            ToolLayoutPanelGroupNode direct => direct,
            ToolLayoutToolNode tool => tool.Group,
            _ => null,
        };

        if (targetGroup is null || ReferenceEquals(targetGroup, group))
        {
            return;
        }

        int index = ViewModel.PanelGroups.IndexOf(targetGroup);
        if (positionInTarget.Y > targetRow.Bounds.Height / 2)
        {
            index++;
        }

        ViewModel.MoveGroupTo(group, index);
    }

    private Control? FindRowAt(Point positionInWindow)
    {
        Control? match = null;
        foreach (Control container in PanelList.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Tag is string tag && tag == RowTag))
        {
            Point? origin = container.TranslatePoint(default, this);
            if (origin is null)
            {
                continue;
            }

            Rect bounds = new(origin.Value, container.Bounds.Size);
            if (bounds.Contains(positionInWindow))
            {
                match = container;
            }
        }

        return match;
    }

    private static Control? FindRow(Visual? source, string tag)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Control { Tag: string rowTag } control && rowTag == tag)
            {
                return control;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(Visual? source)
        where T : Visual
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
