using SmetaNA.EditorUI.Localization;
using SmetaNA.Licensing;

namespace SmetaNA.EditorUI.Licensing;

/// <summary>
/// Локализованные подписи состояний лицензии: сообщения библиотеки
/// SmetaNA.Licensing не показываются напрямую, чтобы сохранить паритет RU/EN.
/// </summary>
public static class LicenseStatusText
{
    /// <summary>Ключ подписи состояния лицензии.</summary>
    public static string ForState(LicenseState state) => state switch
    {
        LicenseState.NotLicensed => LocalizationKeys.LicenseStateNotLicensed,
        LicenseState.NotActivated => LocalizationKeys.LicenseStateNotActivated,
        LicenseState.Licensed => LocalizationKeys.LicenseStateLicensed,
        LicenseState.LicenseExpired => LocalizationKeys.LicenseStateLicenseExpired,
        LicenseState.DeviceMismatch => LocalizationKeys.LicenseStateDeviceMismatch,
        LicenseState.Revoked => LocalizationKeys.LicenseStateRevoked,
        LicenseState.Deactivated => LocalizationKeys.LicenseStateDeactivated,
        LicenseState.TrialActive => LocalizationKeys.LicenseStateTrialActive,
        LicenseState.TrialExpired => LocalizationKeys.LicenseStateTrialExpired,
        LicenseState.InvalidKey => LocalizationKeys.LicenseStateInvalidKey,
        LicenseState.InvalidState => LocalizationKeys.LicenseStateInvalidState,
        _ => LocalizationKeys.LicenseStateNotLicensed,
    };

    /// <summary>Ключ причины отказа лицензионного ключа.</summary>
    public static string ForKeyProblem(LicenseKeyProblem problem) => problem switch
    {
        LicenseKeyProblem.None => string.Empty,
        LicenseKeyProblem.InvalidSignature => LocalizationKeys.LicenseProblemInvalidSignature,
        LicenseKeyProblem.NotYetValid => LocalizationKeys.LicenseProblemNotYetValid,
        LicenseKeyProblem.Expired => LocalizationKeys.LicenseProblemExpired,
        LicenseKeyProblem.LicenseeMismatch => LocalizationKeys.LicenseProblemLicenseeMismatch,
        _ => LocalizationKeys.LicenseProblemInvalidSignature,
    };

    /// <summary>Ключ причины отказа токена активации.</summary>
    public static string ForTokenProblem(TokenProblem problem) => problem switch
    {
        TokenProblem.None => string.Empty,
        TokenProblem.InvalidSignature => LocalizationKeys.LicenseTokenProblemInvalidSignature,
        TokenProblem.DeviceMismatch => LocalizationKeys.LicenseTokenProblemDeviceMismatch,
        TokenProblem.NotYetValid => LocalizationKeys.LicenseTokenProblemNotYetValid,
        TokenProblem.Expired => LocalizationKeys.LicenseTokenProblemExpired,
        TokenProblem.Revoked => LocalizationKeys.LicenseTokenProblemRevoked,
        TokenProblem.KeyMismatch => LocalizationKeys.LicenseTokenProblemKeyMismatch,
        TokenProblem.KeyExpired => LocalizationKeys.LicenseTokenProblemKeyExpired,
        TokenProblem.InvalidKey => LocalizationKeys.LicenseTokenProblemInvalidKey,
        _ => LocalizationKeys.LicenseTokenProblemInvalidSignature,
    };

    /// <summary>Ключ статуса пробного периода.</summary>
    public static string ForTrial(TrialStatusKind kind) => kind switch
    {
        TrialStatusKind.NotStarted => LocalizationKeys.LicenseTrialNotStarted,
        TrialStatusKind.Expired => LocalizationKeys.LicenseStateTrialExpired,
        TrialStatusKind.Tampered or TrialStatusKind.DeviceMismatch => LocalizationKeys.LicenseStateInvalidState,
        _ => LocalizationKeys.LicenseStateTrialActive,
    };
}
