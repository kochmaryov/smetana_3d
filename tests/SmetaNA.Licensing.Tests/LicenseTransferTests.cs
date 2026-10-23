using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Деактивация и перенос лицензии: квитанция, отзыв токена, работа на новом устройстве.</summary>
public sealed class LicenseTransferTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Deactivate_ReturnsReceiptAndDisablesLocalLicense()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        DeactivationReceipt receipt = service.Deactivate();
        DeactivationReceipt parsed = DeactivationReceipt.Parse(receipt.ToReceiptString());

        Assert.Equal(key.Data.KeyId, parsed.KeyId);
        Assert.Equal(token.TokenId, parsed.TokenId);
        Assert.Equal(device.GetFingerprint().BindingHash, parsed.DeviceFingerprintHash);
        Assert.Equal(Start, parsed.DeactivatedAt);
        Assert.Equal(LicenseState.Deactivated, service.GetStatus(key).State);
        Assert.Throws<InvalidOperationException>(() => service.Deactivate());
    }

    [Fact]
    public void Transfer_MovesLicenseToNewDeviceAndRevokesOldActivation()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dirA = new TempStateDirectory();
        using var dirB = new TempStateDirectory();
        var deviceA = new FakeDeviceProvider("machine-a");
        var deviceB = new FakeDeviceProvider("machine-b");
        using var serviceA = new LicenseService(vendor.PublicKeyPem, dirA.Path, deviceA, time);
        using var serviceB = new LicenseService(vendor.PublicKeyPem, dirB.Path, deviceB, time);
        LicenseKey key = vendor.CreateKey(maxActivationsPerYear: 4);

        // Шаг 1: активация на старом устройстве.
        ActivationToken oldToken = vendor.IssueToken(key, deviceA.GetFingerprint().BindingHash);
        Assert.True(serviceA.ApplyActivationToken(oldToken.ToTokenString(), key).Success);

        // Шаг 2: деактивация; квитанция уходит продавцу.
        DeactivationReceipt receipt = serviceA.Deactivate();
        Assert.True(vendor.Issuer.ProcessDeactivation(receipt));

        // Шаг 3: выдача токена новому устройству.
        string requestB = serviceB.CreateActivationRequest(key);
        ActivationIssueResult issuedB = vendor.Issuer.Issue(key, ActivationRequest.Parse(requestB));
        Assert.True(issuedB.Success);
        Assert.True(serviceB.ApplyActivationToken(issuedB.Token!.ToTokenString(), key).Success);

        Assert.Equal(LicenseState.Licensed, serviceB.GetStatus(key).State);

        // Шаг 4: старое устройство помечает лицензию деактивированной; его токен — в списке отзывов.
        RevocationList revocations = vendor.Issuer.CreateRevocationList();
        Assert.Equal(LicenseState.Deactivated, serviceA.GetStatus(key, revocations).State);
        Assert.True(revocations.IsRevoked(oldToken.TokenId));

        // Чужая копия старого токена активацию не применит: node-lock срабатывает раньше отзыва.
        ActivationResult hijack = serviceB.ApplyActivationToken(oldToken.ToTokenString(), key, revocations);
        Assert.False(hijack.Success);
        Assert.Equal(TokenProblem.DeviceMismatch, hijack.Problem);
    }

    [Fact]
    public void IssuerTransfer_RevokesOldTokenAndIssuesNew()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken oldToken = vendor.IssueToken(key, "AAAA1111AAAA1111AAAA1111AAAA1111");
        ActivationRequest request = ActivationRequest.Create(key.Data.KeyId, "BBBB2222BBBB2222BBBB2222BBBB2222", Start);

        ActivationIssueResult result = vendor.Issuer.Transfer(oldToken.TokenId, key, request);

        Assert.True(result.Success);
        Assert.True(vendor.Ledger.IsRevoked(oldToken.TokenId));
        Assert.NotEqual(oldToken.TokenId, result.Token!.TokenId);
        Assert.Equal("BBBB2222BBBB2222BBBB2222BBBB2222", result.Token.DeviceFingerprintHash);
    }

    [Fact]
    public void RevocationList_StopsActivationOnRevokedToken()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        vendor.Issuer.Revoke(token.TokenId, "возврат средств");
        RevocationList revocations = vendor.Issuer.CreateRevocationList();

        // Повторное применение отозванного токена отклоняется.
        ActivationResult reapplied = service.ApplyActivationToken(token.ToTokenString(), key, revocations);
        Assert.False(reapplied.Success);
        Assert.Equal(TokenProblem.Revoked, reapplied.Problem);
        Assert.Equal(LicenseState.Revoked, service.GetStatus(key, revocations).State);
    }
}
