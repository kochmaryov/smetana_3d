using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Node-lock: токен действует только на устройстве, для которого выдан.</summary>
public sealed class NodeLockTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Activation_OnRequestedDevice_ProvidesLicense()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using var service = new LicenseService(vendor.PublicKeyPem, dir.Path, device, time);
        LicenseKey key = vendor.CreateKey();

        string request = service.CreateActivationRequest(key);
        ActivationIssueResult issued = vendor.Issuer.Issue(key, ActivationRequest.Parse(request));
        Assert.True(issued.Success);

        ActivationResult applied = service.ApplyActivationToken(issued.Token!.ToTokenString(), key);

        Assert.True(applied.Success);
        Assert.Equal(TokenProblem.None, applied.Problem);
        Assert.Equal(LicenseState.Licensed, service.GetStatus(key).State);
    }

    [Fact]
    public void Token_ForAnotherDevice_IsRejected()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dirA = new TempStateDirectory();
        using var dirB = new TempStateDirectory();
        var deviceA = new FakeDeviceProvider("machine-a");
        var deviceB = new FakeDeviceProvider("machine-b");
        using var serviceA = new LicenseService(vendor.PublicKeyPem, dirA.Path, deviceA, time);
        using var serviceB = new LicenseService(vendor.PublicKeyPem, dirB.Path, deviceB, time);
        LicenseKey key = vendor.CreateKey();

        ActivationToken token = vendor.IssueToken(key, deviceA.GetFingerprint().BindingHash);
        ActivationResult applied = serviceB.ApplyActivationToken(token.ToTokenString(), key);

        Assert.False(applied.Success);
        Assert.Equal(TokenProblem.DeviceMismatch, applied.Problem);
        Assert.Equal(LicenseState.NotLicensed, serviceB.GetStatus().State);
    }

    [Fact]
    public void ActivationState_CopiedToAnotherDevice_ShowsDeviceMismatch()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dirA = new TempStateDirectory();
        using var dirB = new TempStateDirectory();
        var deviceA = new FakeDeviceProvider("machine-a");
        using var serviceA = new LicenseService(vendor.PublicKeyPem, dirA.Path, deviceA, time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, deviceA.GetFingerprint().BindingHash);
        Assert.True(serviceA.ApplyActivationToken(token.ToTokenString(), key).Success);

        // Копия папки состояния на другую машину (вместе с локальным ключом — как при копировании профиля).
        foreach (string file in Directory.GetFiles(dirA.Path))
        {
            File.Copy(file, Path.Combine(dirB.Path, Path.GetFileName(file)), overwrite: true);
        }

        using var serviceB = new LicenseService(vendor.PublicKeyPem, dirB.Path, new FakeDeviceProvider("machine-b"), time);

        Assert.Equal(LicenseState.DeviceMismatch, serviceB.GetStatus(key).State);
    }

    [Fact]
    public void RequestAndToken_BindToSameDeviceHash()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        var device = new FakeDeviceProvider("machine-a");
        LicenseKey key = vendor.CreateKey();

        ActivationRequest request = ActivationRequest.Create(
            key.Data.KeyId, device.GetFingerprint().BindingHash, Start);
        ActivationRequest parsedRequest = ActivationRequest.Parse(request.ToRequestString());
        ActivationToken token = vendor.IssueToken(key, parsedRequest.DeviceFingerprintHash);
        ActivationToken parsedToken = ActivationToken.Parse(token.ToTokenString());

        Assert.Equal(device.GetFingerprint().BindingHash, parsedRequest.DeviceFingerprintHash);
        Assert.Equal(parsedRequest.DeviceFingerprintHash, parsedToken.DeviceFingerprintHash);
        Assert.Equal(key.Data.KeyId, parsedToken.KeyId);
    }
}
