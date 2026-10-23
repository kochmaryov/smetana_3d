using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Работа без сети (DoD §5.8): активация живёт долго, проверки полностью локальные.</summary>
public sealed class OfflineWorkTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PerpetualLicense_WorksAfter45DaysWithoutNetwork()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        // Ни одного обращения к сети: всё работает по локальному состоянию.
        time.Advance(TimeSpan.FromDays(45));

        LicenseStatus status = service.GetStatus(key);
        Assert.Equal(LicenseState.Licensed, status.State);
        Assert.False(status.ClockRollbackDetected);
        Assert.Null(status.ValidUntil);
    }

    [Fact]
    public void SubscriptionLicense_WorksUntilExpiryThenExpires()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        DateTimeOffset expiry = Start.AddDays(60);
        LicenseKey key = vendor.CreateKey(kind: LicenseKind.Subscription, expiresAt: expiry);
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        time.Advance(TimeSpan.FromDays(30));
        Assert.Equal(LicenseState.Licensed, service.GetStatus(key).State);

        time.Advance(TimeSpan.FromDays(31));
        LicenseStatus expired = service.GetStatus(key);
        Assert.Equal(LicenseState.LicenseExpired, expired.State);
        Assert.Equal(expiry, expired.ValidUntil);
    }

    [Fact]
    public void ActivationToken_CoversAtLeastThirtyDaysOffline()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        var device = new FakeDeviceProvider("machine-a");

        LicenseKey perpetualKey = vendor.CreateKey();
        ActivationToken perpetualToken = vendor.IssueToken(perpetualKey, device.GetFingerprint().BindingHash);
        Assert.Null(perpetualToken.ValidUntil);

        DateTimeOffset farExpiry = Start.AddDays(365);
        LicenseKey subscriptionKey = vendor.CreateKey(kind: LicenseKind.Subscription, expiresAt: farExpiry);
        ActivationToken subscriptionToken = vendor.IssueToken(subscriptionKey, device.GetFingerprint().BindingHash);

        Assert.NotNull(subscriptionToken.ValidUntil);
        Assert.True(subscriptionToken.ValidUntil!.Value - Start >= TimeSpan.FromDays(30));
    }

    [Fact]
    public void ExplicitTokenTtl_CapsValidityButKeepsThirtyDayFloor()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var issuer = new ActivationTokenIssuer(
            vendor.SigningKey,
            vendor.Ledger,
            time,
            tokenTtl: TimeSpan.FromDays(90));
        var device = new FakeDeviceProvider("machine-a");
        LicenseKey key = vendor.CreateKey();

        ActivationIssueResult result = issuer.Issue(
            key,
            ActivationRequest.Create(key.Data.KeyId, device.GetFingerprint().BindingHash, Start));

        Assert.True(result.Success);
        Assert.Equal(Start.AddDays(90), result.Token!.ValidUntil);
    }

    [Fact]
    public void OfflineStatus_DoesNotRequireAnyKeyMaterialBeyondPublic()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        // Клиент знает только открытый ключ продавца — приватный ключ и журнал ему недоступны.
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        time.Advance(TimeSpan.FromDays(30));
        Assert.Equal(LicenseState.Licensed, service.GetStatus().State);
    }
}
