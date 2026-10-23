using SmetaNA.EditorUI.Commands;
using SmetaNA.EditorUI.Licensing;
using SmetaNA.EditorUI.Localization;
using SmetaNA.Licensing;

namespace SmetaNA.EditorUI.ViewModels;

/// <summary>
/// Экран лицензирования (задача 6.4): проверка ключа, офлайн-активация токеном,
/// статус триала и деактивация для переноса. Вся работа локальная — без сети
/// (DoD §5.8); логика не зависит от Avalonia и тестируется юнитами.
/// </summary>
public sealed class LicenseWindowViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly LicenseService _service;
    private readonly string _vendorPublicKeyPem;
    private readonly TimeProvider _time;

    private string _keyInput = string.Empty;
    private string _tokenInput = string.Empty;
    private string _feedbackText = string.Empty;
    private string _activationRequestText = string.Empty;
    private string _receiptText = string.Empty;

    public LicenseWindowViewModel(
        ILocalizationService localization,
        LicenseService service,
        string vendorPublicKeyPem,
        bool isDevKey = false,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorPublicKeyPem);
        _localization = localization;
        _service = service;
        _vendorPublicKeyPem = vendorPublicKeyPem;
        _time = time ?? TimeProvider.System;
        IsDevKey = isDevKey;

        Strings = new LocalizedStrings(localization);

        ValidateKeyCommand = new RelayCommand(_ => ValidateKey(), _ => KeyInput.Trim().Length > 0);
        ApplyTokenCommand = new RelayCommand(_ => ApplyToken(), _ => TokenInput.Trim().Length > 0);
        StartTrialCommand = new RelayCommand(_ => StartTrial(), _ => CanStartTrial);
        DeactivateCommand = new RelayCommand(_ => Deactivate(), _ => CanDeactivate);
        RefreshCommand = new RelayCommand(_ => Refresh());

        Refresh();
        _localization.CultureChanged += OnCultureChanged;
    }

    public LocalizedStrings Strings { get; }

    /// <summary>Используется временный ключ разработчика (предупреждение на экране).</summary>
    public bool IsDevKey { get; }

    public string KeyInput
    {
        get => _keyInput;
        set
        {
            if (SetField(ref _keyInput, value))
            {
                ValidateKeyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TokenInput
    {
        get => _tokenInput;
        set
        {
            if (SetField(ref _tokenInput, value))
            {
                ApplyTokenCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Итог последнего действия (проверка ключа, активация, деактивация).</summary>
    public string FeedbackText
    {
        get => _feedbackText;
        private set => SetField(ref _feedbackText, value);
    }

    /// <summary>Запрос активации для продавца (после успешной проверки ключа).</summary>
    public string ActivationRequestText
    {
        get => _activationRequestText;
        private set
        {
            if (SetField(ref _activationRequestText, value))
            {
                OnPropertyChanged(nameof(HasActivationRequest));
            }
        }
    }

    public bool HasActivationRequest => ActivationRequestText.Length > 0;

    /// <summary>Квитанция деактивации для продавца (перенос лицензии).</summary>
    public string ReceiptText
    {
        get => _receiptText;
        private set
        {
            if (SetField(ref _receiptText, value))
            {
                OnPropertyChanged(nameof(HasReceipt));
            }
        }
    }

    public bool HasReceipt => ReceiptText.Length > 0;

    /// <summary>Локализованное состояние лицензии.</summary>
    public string StatusText { get; private set; } = string.Empty;

    /// <summary>Текущее состояние лицензии.</summary>
    public LicenseState State { get; private set; } = LicenseState.NotLicensed;

    /// <summary>Требует ли лицензия внимания (экран показывается при старте).</summary>
    public bool RequiresAttention =>
        State is not (LicenseState.Licensed or LicenseState.TrialActive);

    /// <summary>Срок действия лицензии/активации; пусто — если не ограничен.</summary>
    public string ValidUntilText { get; private set; } = string.Empty;

    /// <summary>Статус пробного периода (дней осталось / до даты).</summary>
    public string TrialText { get; private set; } = string.Empty;

    /// <summary>Отпечаток устройства для запроса в поддержку.</summary>
    public string DeviceHash { get; private set; } = string.Empty;

    public bool CanStartTrial { get; private set; }

    public bool CanDeactivate { get; private set; }

    public RelayCommand ValidateKeyCommand { get; }
    public RelayCommand ApplyTokenCommand { get; }
    public RelayCommand StartTrialCommand { get; }
    public RelayCommand DeactivateCommand { get; }
    public RelayCommand RefreshCommand { get; }

    /// <summary>Проверить введённый ключ и сформировать запрос активации.</summary>
    public bool ValidateKey()
    {
        string text = KeyInput.Trim();
        if (text.Length == 0)
        {
            FeedbackText = _localization.Get(LocalizationKeys.LicenseKeyEmpty);
            return false;
        }

        if (!LicenseKey.TryParse(text, out LicenseKey? key) || key is null)
        {
            FeedbackText = FormatInvalidKey(LocalizationKeys.LicenseProblemMalformed);
            return false;
        }

        using var verifier = new LicenseKeyVerifier(_vendorPublicKeyPem);
        LicenseKeyValidationResult check = verifier.Validate(key, _time.GetUtcNow());
        if (!check.IsValid)
        {
            ActivationRequestText = string.Empty;
            FeedbackText = FormatInvalidKey(LicenseStatusText.ForKeyProblem(check.Problem));
            return false;
        }

        ActivationRequestText = _service.CreateActivationRequest(key);
        FeedbackText = _localization.Get(LocalizationKeys.LicenseKeyValid);
        return true;
    }

    /// <summary>Применить токен активации от продавца (офлайн).</summary>
    public bool ApplyToken()
    {
        string text = TokenInput.Trim();
        if (text.Length == 0)
        {
            FeedbackText = _localization.Get(LocalizationKeys.LicenseTokenEmpty);
            return false;
        }

        LicenseKey? key = LicenseKey.TryParse(KeyInput.Trim(), out LicenseKey? parsed) ? parsed : null;
        ActivationResult result = _service.ApplyActivationToken(text, key);
        if (!result.Success)
        {
            FeedbackText = string.Format(
                _localization.Get(LocalizationKeys.LicenseTokenFailed),
                _localization.Get(LicenseStatusText.ForTokenProblem(result.Problem)));
            Refresh();
            return false;
        }

        FeedbackText = _localization.Get(LocalizationKeys.LicenseTokenApplied);
        Refresh();
        return true;
    }

    /// <summary>Запустить пробный период (идемпотентно).</summary>
    public bool StartTrial()
    {
        if (!CanStartTrial)
        {
            return false;
        }

        TrialStatus trial = _service.StartTrial();
        FeedbackText = _localization.Get(LocalizationKeys.LicenseTrialStarted);
        Refresh();
        return trial.Kind == TrialStatusKind.Active;
    }

    /// <summary>Деактивировать лицензию и получить квитанцию для переноса.</summary>
    public bool Deactivate()
    {
        if (!CanDeactivate)
        {
            return false;
        }

        try
        {
            DeactivationReceipt receipt = _service.Deactivate();
            ReceiptText = receipt.ToReceiptString();
            FeedbackText = _localization.Get(LocalizationKeys.LicenseDeactivated);
            Refresh();
            return true;
        }
        catch (InvalidOperationException)
        {
            LicenseStatus status = _service.GetStatus();
            FeedbackText = string.Format(
                _localization.Get(LocalizationKeys.LicenseDeactivateFailed),
                _localization.Get(LicenseStatusText.ForState(status.State)));
            return false;
        }
    }

    /// <summary>Перечитать статус лицензии, триала и устройства.</summary>
    public void Refresh()
    {
        LicenseStatus status = _service.GetStatus();
        State = status.State;
        StatusText = _localization.Get(LicenseStatusText.ForState(status.State));
        ValidUntilText = status.ValidUntil is { } validUntil
            ? string.Format(
                _localization.Get(LocalizationKeys.LicenseValidUntil),
                validUntil.ToLocalTime().ToString("d"))
            : string.Empty;

        TrialStatus trial = _service.GetTrialStatus();
        TrialText = FormatTrial(trial);

        DeviceHash = _service.GetDeviceFingerprint().BindingHash;
        CanStartTrial = trial.Kind == TrialStatusKind.NotStarted;
        CanDeactivate = status.State is LicenseState.Licensed
            or LicenseState.LicenseExpired
            or LicenseState.Deactivated
            or LicenseState.Revoked;

        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(RequiresAttention));
        OnPropertyChanged(nameof(ValidUntilText));
        OnPropertyChanged(nameof(TrialText));
        OnPropertyChanged(nameof(DeviceHash));
        OnPropertyChanged(nameof(CanStartTrial));
        OnPropertyChanged(nameof(CanDeactivate));
        StartTrialCommand.RaiseCanExecuteChanged();
        DeactivateCommand.RaiseCanExecuteChanged();
    }

    private string FormatTrial(TrialStatus trial)
    {
        if (trial.Kind == TrialStatusKind.Active && trial.ExpiresAt is { } expiresAt)
        {
            int daysLeft = Math.Max(0, (int)Math.Ceiling((expiresAt - _time.GetUtcNow()).TotalDays));
            return string.Join(
                " · ",
                string.Format(_localization.Get(LocalizationKeys.LicenseTrialDaysLeft), daysLeft),
                string.Format(_localization.Get(LocalizationKeys.LicenseTrialUntil), expiresAt.ToLocalTime().ToString("d")));
        }

        return _localization.Get(LicenseStatusText.ForTrial(trial.Kind));
    }

    private string FormatInvalidKey(string problemKey) =>
        string.Format(_localization.Get(LocalizationKeys.LicenseKeyInvalid), _localization.Get(problemKey));

    private void OnCultureChanged()
    {
        Refresh();
    }
}
