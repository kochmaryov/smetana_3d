using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Фальсификация и битые подписи: ключ, токен, список отзывов, локальное состояние.</summary>
public sealed class ForgeryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Key_WithTamperedSignature_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var verifier = new LicenseKeyVerifier(vendor.PublicKeyPem);
        LicenseKey key = vendor.CreateKey();

        byte[] forgedSignature = key.Signature;
        forgedSignature[0] ^= 0xFF;
        var forged = new LicenseKey(key.Data, forgedSignature);

        LicenseKeyValidationResult result = verifier.Validate(forged, Start);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseKeyProblem.InvalidSignature, result.Problem);
    }

    [Fact]
    public void Key_WithTamperedPayload_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var verifier = new LicenseKeyVerifier(vendor.PublicKeyPem);
        LicenseKey key = vendor.CreateKey();

        // Данные изменены (например, хеш лицензиата), подпись осталась прежней.
        byte[] payload = key.Data.ToPayloadBytes();
        payload[29] ^= 0x01;
        LicenseKey forged = LicenseKey.Parse(
            LicenseText.Format(LicenseKey.KeyPrefix, payload, key.SignatureCore));

        LicenseKeyValidationResult result = verifier.Validate(forged, Start);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseKeyProblem.InvalidSignature, result.Problem);
    }

    [Fact]
    public void Key_SignedByAnotherVendor_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        using var verifier = new LicenseKeyVerifier(vendor.PublicKeyPem);
        LicenseKey foreignKey = impostor.CreateKey();

        LicenseKeyValidationResult result = verifier.Validate(foreignKey, Start);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseKeyProblem.InvalidSignature, result.Problem);
    }

    [Fact]
    public void Token_WithTamperedSignature_FailsVerificationAndActivation()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var verifier = new ActivationTokenVerifier(vendor.PublicKeyPem);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        byte[] forgedSignature = token.Signature;
        forgedSignature[3] ^= 0x55;
        var forged = new ActivationToken(
            token.TokenId,
            token.KeyId,
            token.IssuedAt,
            token.ValidUntil,
            token.ActivationOrdinal,
            token.DeviceFingerprintHash,
            forgedSignature);

        Assert.False(verifier.VerifySignature(forged));

        ActivationResult applied = service.ApplyActivationToken(forged.ToTokenString(), key);

        Assert.False(applied.Success);
        Assert.Equal(TokenProblem.InvalidSignature, applied.Problem);
    }

    [Fact]
    public void Token_WithTamperedPayload_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var verifier = new ActivationTokenVerifier(vendor.PublicKeyPem);
        LicenseKey key = vendor.CreateKey();
        var device = new FakeDeviceProvider("machine-a");
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        byte[] payload = token.ToPayloadBytes();
        payload[35] ^= 0x01; // поле даты выдачи
        ActivationToken forged = ActivationToken.Parse(
            LicenseText.Format(ActivationToken.TokenPrefix, payload, token.SignatureCore));

        TokenValidationResult result = verifier.Validate(forged, device.GetFingerprint().BindingHash, Start);

        Assert.False(result.IsValid);
        Assert.Equal(TokenProblem.InvalidSignature, result.Problem);
    }

    [Fact]
    public void RevocationList_WithTamperedPayload_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        RevocationList list = vendor.Issuer.CreateRevocationList();
        string envelope = list.ToEnvelopeString();

        (byte[] payload, byte[] signature) = SignedEnvelope.Read(envelope, RevocationList.EnvelopeFormat);
        payload[^1] ^= 0x01;
        string forgedEnvelope = SignedEnvelope.Write(RevocationList.EnvelopeFormat, payload, signature);

        Assert.Throws<LicenseFormatException>(() => RevocationList.Parse(forgedEnvelope, vendor.PublicKeyPem));
    }

    [Fact]
    public void RevocationList_SignedByAnotherVendor_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        RevocationList foreignList = impostor.Issuer.CreateRevocationList();

        Assert.Throws<LicenseFormatException>(
            () => RevocationList.Parse(foreignList.ToEnvelopeString(), vendor.PublicKeyPem));
    }

    [Fact]
    public void ActivationState_WithEditedFields_IsDetected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        // «Правка в текстовом редакторе»: содержимое меняется, подпись остаётся прежней.
        string path = Path.Combine(dir.Path, LocalStateStore.ActivationStateFile);
        (byte[] payload, byte[] signature) = SignedEnvelope.Read(File.ReadAllText(path), "smetana-activation-state/1");
        string edited = Encoding.UTF8.GetString(payload).Replace("\"deactivated\":false", "\"deactivated\":true");
        File.WriteAllText(path, SignedEnvelope.Write("smetana-activation-state/1", Encoding.UTF8.GetBytes(edited), signature));

        LicenseStatus status = service.GetStatus(key);

        Assert.Equal(LicenseState.InvalidState, status.State);
    }

    [Fact]
    public void TrialState_WithEditedFields_IsDetected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);
        Assert.Equal(TrialStatusKind.Active, service.StartTrial().Kind);

        string path = Path.Combine(dir.Path, LocalStateStore.TrialStateFile);
        (byte[] payload, byte[] signature) = SignedEnvelope.Read(File.ReadAllText(path), "smetana-trial-state/1");
        string edited = Encoding.UTF8.GetString(payload).Replace("\"formatVersion\":1", "\"formatVersion\":2");
        File.WriteAllText(path, SignedEnvelope.Write("smetana-trial-state/1", Encoding.UTF8.GetBytes(edited), signature));

        TrialStatus trial = service.GetTrialStatus();

        Assert.Equal(TrialStatusKind.Tampered, trial.Kind);
        Assert.Equal(LicenseState.InvalidState, service.GetStatus().State);
    }

    [Fact]
    public void LocalState_ReSignedWithForeignKey_IsDetected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        // Подделка «в лоб»: новый локальный ключ и новый конверт с чужой подписью.
        (byte[] payload, _) = SignedEnvelope.Read(
            File.ReadAllText(Path.Combine(dir.Path, LocalStateStore.ActivationStateFile)),
            "smetana-activation-state/1");

        using ECDsa attackerKey = LicenseKeyGenerator.CreateSigningKey();
        byte[] attackerSignature = LicenseCrypto.Sign(attackerKey, "smetana-activation-state/1", payload);
        File.WriteAllText(
            Path.Combine(dir.Path, LocalStateStore.ActivationStateFile),
            SignedEnvelope.Write("smetana-activation-state/1", payload, attackerSignature));

        Assert.Equal(LicenseState.InvalidState, service.GetStatus(key).State);
    }

    [Fact]
    public void GarbageStrings_AreRejectedByParsers()
    {
        Assert.False(ActivationToken.TryParse("SMNA1.XXXX", out ActivationToken? token));
        Assert.Null(token);
        Assert.False(ActivationRequest.TryParse("SMNQ1.###", out ActivationRequest? request));
        Assert.Null(request);
        Assert.Throws<LicenseFormatException>(() => DeactivationReceipt.Parse("SMND1.???"));
    }
}
