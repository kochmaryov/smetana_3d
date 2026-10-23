using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Поведение фасада LicenseService: статусы, запрос активации, совместимость ключа и токена.</summary>
public sealed class LicenseServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetStatus_WithoutAnything_IsNotLicensed()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);

        Assert.Equal(LicenseState.NotLicensed, service.GetStatus().State);
    }

    [Fact]
    public void GetStatus_WithValidKey_IsNotActivated()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);
        LicenseKey key = vendor.CreateKey();

        LicenseStatus status = service.GetStatus(key);

        Assert.Equal(LicenseState.NotActivated, status.State);
        Assert.Equal(key.Data.KeyId, status.KeyId);
    }

    [Fact]
    public void GetStatus_WithForgedKey_IsInvalidKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);

        LicenseStatus status = service.GetStatus(impostor.CreateKey());

        Assert.Equal(LicenseState.InvalidKey, status.State);
    }

    [Fact]
    public void CreateActivationRequest_RejectsInvalidKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);

        Assert.Throws<LicenseFormatException>(() => service.CreateActivationRequest(impostor.CreateKey()));
    }

    [Fact]
    public void CreateActivationRequest_CarriesKeyAndDevice()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();

        ActivationRequest request = ActivationRequest.Parse(service.CreateActivationRequest(key));

        Assert.Equal(key.Data.KeyId, request.KeyId);
        Assert.Equal(device.GetFingerprint().BindingHash, request.DeviceFingerprintHash);
        Assert.Equal(Start, request.RequestedAt);
    }

    [Fact]
    public void ApplyActivationToken_RejectsTokenForAnotherKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();
        LicenseKey otherKey = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        ActivationResult applied = service.ApplyActivationToken(token.ToTokenString(), otherKey);

        Assert.False(applied.Success);
        Assert.Equal(TokenProblem.KeyMismatch, applied.Problem);
    }

    [Fact]
    public void ApplyActivationToken_RejectsGarbage()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using var service = new LicenseService(
            vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time);

        ActivationResult applied = service.ApplyActivationToken("SMNA1.aaaa-bbbb.cccc-dddd");

        Assert.False(applied.Success);
        Assert.Equal(LicenseState.NotLicensed, service.GetStatus().State);
    }

    [Fact]
    public void DeviceFingerprint_IsExposedForSupport()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);

        DeviceFingerprint fingerprint = service.GetDeviceFingerprint();

        Assert.Equal(device.GetFingerprint().BindingHash, fingerprint.BindingHash);
        Assert.Equal(32, fingerprint.BindingHash.Length);
    }

    [Fact]
    public void LocalStateStore_KeyAndStates_ArePersistentAcrossRestarts()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);

        using (var firstRun = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time))
        {
            Assert.True(firstRun.ApplyActivationToken(token.ToTokenString(), key).Success);
        }

        // «Перезапуск приложения»: состояние и локальный ключ переживают новый экземпляр сервиса.
        using var secondRun = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        Assert.Equal(LicenseState.Licensed, secondRun.GetStatus(key).State);
    }
}
