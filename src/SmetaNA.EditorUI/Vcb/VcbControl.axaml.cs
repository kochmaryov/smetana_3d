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
        UpdateUnitLabel();
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
        SetError(string.Empty);
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
        else if (change.Property == UnitProperty)
        {
            UpdateUnitLabel();
        }
    }

    /// <summary>Единица измерения после значения (как в макетах варианта A).</summary>
    private void UpdateUnitLabel()
    {
        if (UnitLabel is null)
        {
            return;
        }

        UnitLabel.Text = EditorServices.Localization.Get(LocalizationKeys.ForUnit(Unit));
    }

    /// <summary>Показать/снять сообщение об ошибке (красная рамка строки VCB).</summary>
    private void SetError(string message)
    {
        ErrorText.Text = message;
        if (message.Length > 0)
        {
            RootBorder.Classes.Add("error");
        }
        else
        {
            RootBorder.Classes.Remove("error");
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
        SetError(string.Empty);

    private void Commit()
    {
        ILocalizationService localization = EditorServices.Localization;
        // VCB принимает и числа, и координаты независимо от объявленного типа:
        // Move понимает длину, Line/Rectangle/Circle — абсолютные и относительные точки.
        if (Kind == VcbInputKind.Coordinate)
        {
            VcbCoordinateResult result = VcbValueParser.ParseCoordinate(InputBox.Text, Unit);
            if (result.IsSuccess)
            {
                SetError(string.Empty);
                Committed?.Invoke(this, VcbCommittedEventArgs.ForCoordinate(result));
                return;
            }

            VcbParseResult fallback = VcbValueParser.ParseScalar(InputBox.Text, VcbInputKind.Length, Unit);
            if (fallback.IsSuccess)
            {
                SetError(string.Empty);
                Committed?.Invoke(this, VcbCommittedEventArgs.ForScalar(VcbInputKind.Length, fallback));
                return;
            }

            SetError(localization.Get(LocalizationKeys.ForVcbError(result.Error)));
            return;
        }

        VcbParseResult resultScalar = VcbValueParser.ParseScalar(InputBox.Text, Kind, Unit);
        if (resultScalar.IsSuccess)
        {
            SetError(string.Empty);
            Committed?.Invoke(this, VcbCommittedEventArgs.ForScalar(Kind, resultScalar));
            return;
        }

        VcbCoordinateResult coordinate = VcbValueParser.ParseCoordinate(InputBox.Text, Unit);
        if (coordinate.IsSuccess)
        {
            SetError(string.Empty);
            Committed?.Invoke(this, VcbCommittedEventArgs.ForCoordinate(coordinate));
            return;
        }

        SetError(localization.Get(LocalizationKeys.ForVcbError(resultScalar.Error)));
    }
}
