using System.Security.Cryptography;
using SmetaNA.EditorUI.Licensing;
using SmetaNA.EditorUI.Localization;
using SmetaNA.EditorUI.ViewModels;
using SmetaNA.Licensing;
using Xunit;

namespace SmetaNA.EditorUI.Tests;

/// <summary>
/// Проверки экрана лицензирования (задача 6.4): ввод и проверка ключа,
/// офлайн-активация токеном, триал, деактивация для переноса (DoD §5.8).
/// </summary>
public sealed class LicenseWindowViewModelTests : IDisposable
{
    private readonly string _stateDirectory;
    private readonly ECDsa _vendorKey = LicenseKeyGenerator.CreateSigningKey();
    private readonly LicenseKeyGenerator _generator;
    private readonly string _vendorPublicPem;

    public LicenseWindowViewModelTests()
    {
        _stateDirectory = Path.Combine(Path.GetTempPath(), "smetana-license-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_stateDirectory);
        _generator = new LicenseKeyGenerator(_vendorKey);
        _vendorPublicPem = _generator.ExportPublicKeyPem();
    }

    public void Dispose()
    {
        _generator.Dispose();
        _vendorKey.Dispose();
        try
        {
            Directory.Delete(_stateDirectory, recursive: true);
        }
        catch (IOException)
        {
            // временный каталог удалится системой
        }
    }

    private LicenseService CreateService() => new(_vendorPublicPem, _stateDirectory);

    private LicenseWindowViewModel CreateViewModel(LicenseService service, LocalizationService? localization = null) =>
        new(localization ?? new LocalizationService(), service, _vendorPublicPem, isDevKey: true);

    private LicenseKey CreateKey(DateTimeOffset? expiresAt = null) =>
        _generator.Generate(new LicenseKeyData(
            Guid.NewGuid(),
            LicenseKind.Perpetual,
            DateTimeOffset.UtcNow.AddDays(-1),
            expiresAt,
            maxActivationsPerYear: 3));

    [Fact]
    public void ValidateKey_MalformedText_ReportsLocalizedError()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();

        viewModel.KeyInput = "это не ключ";
        Assert.False(viewModel.ValidateKey());

        string expectedProblem = localization.Get(LocalizationKeys.LicenseProblemMalformed);
        string expected = string.Format(localization.Get(LocalizationKeys.LicenseKeyInvalid), expectedProblem);
        Assert.Equal(expected, viewModel.FeedbackText);
        Assert.Empty(viewModel.ActivationRequestText);
    }

    [Fact]
    public void ValidateKey_ForgedSignature_IsRejected()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        using ECDsa attacker = LicenseKeyGenerator.CreateSigningKey();
        using var forged = new LicenseKeyGenerator(attacker);
        LicenseKey key = forged.Generate(new LicenseKeyData(
            Guid.NewGuid(),
            LicenseKind.Perpetual,
            DateTimeOffset.UtcNow.AddDays(-1),
            null,
            maxActivationsPerYear: 3));
        var localization = new LocalizationService();

        viewModel.KeyInput = key.ToLicenseString();
        Assert.False(viewModel.ValidateKey());
        Assert.Empty(viewModel.ActivationRequestText);

        string expectedProblem = localization.Get(LocalizationKeys.LicenseProblemInvalidSignature);
        string expected = string.Format(localization.Get(LocalizationKeys.LicenseKeyInvalid), expectedProblem);
        Assert.Equal(expected, viewModel.FeedbackText);
    }

    [Fact]
    public void ValidateKey_ValidKey_CreatesOfflineActivationRequest()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();

        viewModel.KeyInput = CreateKey().ToLicenseString();
        Assert.True(viewModel.ValidateKeyCommand.CanExecute(null));
        Assert.True(viewModel.ValidateKey());

        Assert.StartsWith(ActivationRequest.RequestPrefix, viewModel.ActivationRequestText, StringComparison.Ordinal);
        Assert.True(viewModel.HasActivationRequest);
        Assert.Equal(localization.Get(LocalizationKeys.LicenseKeyValid), viewModel.FeedbackText);
    }

    [Fact]
    public void ApplyToken_ActivatesLicense_Offline()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();
        LicenseKey key = CreateKey();

        viewModel.KeyInput = key.ToLicenseString();
        Assert.True(viewModel.ValidateKey());

        ActivationRequest request = ActivationRequest.Parse(viewModel.ActivationRequestText);
        using var issuer = new ActivationTokenIssuer(_vendorKey, new ActivationLedger());
        ActivationIssueResult issued = issuer.Issue(key, request);
        Assert.True(issued.Success);

        viewModel.TokenInput = issued.Token!.ToTokenString();
        Assert.True(viewModel.ApplyToken());

        Assert.Equal(LicenseState.Licensed, viewModel.State);
        Assert.False(viewModel.RequiresAttention);
        Assert.Equal(localization.Get(LocalizationKeys.LicenseTokenApplied), viewModel.FeedbackText);
    }

    [Fact]
    public void ApplyToken_InvalidToken_ReportsLocalizedProblem()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();

        viewModel.TokenInput = "SMNT1-мусор";
        Assert.False(viewModel.ApplyToken());

        string expected = string.Format(
            localization.Get(LocalizationKeys.LicenseTokenFailed),
            localization.Get(LocalizationKeys.LicenseTokenProblemInvalidSignature));
        Assert.Equal(expected, viewModel.FeedbackText);
    }

    [Fact]
    public void StartTrial_ShowsStatus_AndDisablesRepeat()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();

        Assert.True(viewModel.CanStartTrial);
        Assert.True(viewModel.StartTrial());

        Assert.False(viewModel.CanStartTrial);
        Assert.False(viewModel.StartTrialCommand.CanExecute(null));
        Assert.False(viewModel.RequiresAttention);
        Assert.Equal(LicenseState.TrialActive, viewModel.State);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.TrialText));
        Assert.Equal(localization.Get(LocalizationKeys.LicenseTrialStarted), viewModel.FeedbackText);
    }

    [Fact]
    public void Deactivate_ProducesReceipt_ForLicenseTransfer()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);
        var localization = new LocalizationService();
        LicenseKey key = CreateKey();

        viewModel.KeyInput = key.ToLicenseString();
        Assert.True(viewModel.ValidateKey());
        ActivationRequest request = ActivationRequest.Parse(viewModel.ActivationRequestText);
        using var issuer = new ActivationTokenIssuer(_vendorKey, new ActivationLedger());
        ActivationIssueResult issued = issuer.Issue(key, request);
        Assert.True(issued.Success);
        viewModel.TokenInput = issued.Token!.ToTokenString();
        Assert.True(viewModel.ApplyToken());

        Assert.True(viewModel.CanDeactivate);
        Assert.True(viewModel.Deactivate());

        Assert.True(viewModel.HasReceipt);
        Assert.Equal(LicenseState.Deactivated, viewModel.State);
        Assert.True(viewModel.RequiresAttention);
        Assert.Equal(localization.Get(LocalizationKeys.LicenseDeactivated), viewModel.FeedbackText);
    }

    [Fact]
    public void Deactivate_WithoutActivation_IsUnavailable()
    {
        using LicenseService service = CreateService();
        LicenseWindowViewModel viewModel = CreateViewModel(service);

        Assert.False(viewModel.CanDeactivate);
        Assert.False(viewModel.DeactivateCommand.CanExecute(null));
        Assert.False(viewModel.Deactivate());
    }

    [Fact]
    public void Feedback_IsLocalized_InBothCultures()
    {
        using LicenseService service = CreateService();
        var localization = new LocalizationService();
        LicenseWindowViewModel viewModel = CreateViewModel(service, localization);

        viewModel.KeyInput = "плохой ключ";
        viewModel.ValidateKey();
        string russian = viewModel.FeedbackText;

        localization.SetCulture(EditorCulture.English);
        viewModel.ValidateKey();
        string english = viewModel.FeedbackText;

        Assert.NotEqual(russian, english);
    }

    [Fact]
    public void StatusAndTrial_AreLocalized_InBothCultures()
    {
        using LicenseService service = CreateService();
        var localization = new LocalizationService();
        LicenseWindowViewModel viewModel = CreateViewModel(service, localization);
        viewModel.StartTrial();

        localization.SetCulture(EditorCulture.Russian);
        string russian = viewModel.StatusText + viewModel.TrialText;

        localization.SetCulture(EditorCulture.English);
        string english = viewModel.StatusText + viewModel.TrialText;

        Assert.NotEqual(russian, english);
    }

    [Fact]
    public void LicenseStatusText_MapsEveryStateToKey()
    {
        var localization = new LocalizationService();
        foreach (LicenseState state in Enum.GetValues<LicenseState>())
        {
            string key = LicenseStatusText.ForState(state);
            Assert.NotEqual(key, localization.Get(key));
        }

        foreach (LicenseKeyProblem problem in Enum.GetValues<LicenseKeyProblem>())
        {
            string key = LicenseStatusText.ForKeyProblem(problem);
            if (problem == LicenseKeyProblem.None)
            {
                Assert.Equal(string.Empty, key);
                continue;
            }

            Assert.NotEqual(key, localization.Get(key));
        }

        foreach (TokenProblem problem in Enum.GetValues<TokenProblem>())
        {
            string key = LicenseStatusText.ForTokenProblem(problem);
            if (problem == TokenProblem.None)
            {
                Assert.Equal(string.Empty, key);
                continue;
            }

            Assert.NotEqual(key, localization.Get(key));
        }
    }
}
