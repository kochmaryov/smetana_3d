using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmetaNA.Core.Model;
using SmetaNA.EditorUI.Localization;

namespace SmetaNA.EditorUI.Vcb;

/// <summary>
/// Переиспользуемая строка числового ввода (VCB) с валидацией:
/// длина/радиус/угол/координаты, ввод по Enter, сброс по Escape,
/// локализованные ошибки (спека §3.2 п. 3).
/// </summary>
public partial class VcbControl : UserControl
{
    public static readonly StyledProperty<string> PromptProperty =
        AvaloniaProperty.Register<VcbControl, string>(nameof(Prompt), string.Empty);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<VcbControl, string>(nameof(PlaceholderText), string.Empty);

    public static readonly StyledProperty<VcbInputKind> KindProperty =
        AvaloniaProperty.Register<VcbControl, VcbInputKind>(nameof(Kind), VcbInputKind.Length);

    public static readonly StyledProperty<LengthUnit> UnitProperty =
        AvaloniaProperty.Register<VcbControl, LengthUnit>(nameof(Unit), LengthUnit.Millimeter);

    public VcbControl()
    {
        InitializeComponent();
    }

    /// <summary>Подсказка перед полем ввода («Длина», «Радиус», ...).</summary>
    public string Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    /// <summary>Подсказка-заполнитель поля ввода.</summary>
    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Тип проверяемого значения.</summary>
    public VcbInputKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Единицы длины проекта (для длины/радиуса/координат).</summary>
    public LengthUnit Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Значение разобрано и прошло валидацию.</summary>
    public event EventHandler<VcbCommittedEventArgs>? Committed;

    /// <summary>Очистить поле ввода и ошибку.</summary>
    public void ResetInput()
    {
        InputBox.Text = string.Empty;
        ErrorText.Text = string.Empty;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (PromptText is null || InputBox is null)
        {
            return;
        }

        if (change.Property == PromptProperty)
        {
            PromptText.Text = Prompt;
        }
        else if (change.Property == PlaceholderTextProperty)
        {
            InputBox.PlaceholderText = PlaceholderText;
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ResetInput();
            e.Handled = true;
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e) =>
        ErrorText.Text = string.Empty;

    private void Commit()
    {
        ILocalizationService localization = EditorServices.Localization;
        if (Kind == VcbInputKind.Coordinate)
        {
            VcbCoordinateResult result = VcbValueParser.ParseCoordinate(InputBox.Text, Unit);
            if (!result.IsSuccess)
            {
                ErrorText.Text = localization.Get(LocalizationKeys.ForVcbError(result.Error));
                return;
            }

            ErrorText.Text = string.Empty;
            Committed?.Invoke(this, VcbCommittedEventArgs.ForCoordinate(result));
            return;
        }

        VcbParseResult resultScalar = VcbValueParser.ParseScalar(InputBox.Text, Kind, Unit);
        if (!resultScalar.IsSuccess)
        {
            ErrorText.Text = localization.Get(LocalizationKeys.ForVcbError(resultScalar.Error));
            return;
        }

        ErrorText.Text = string.Empty;
        Committed?.Invoke(this, VcbCommittedEventArgs.ForScalar(Kind, resultScalar));
    }
}
