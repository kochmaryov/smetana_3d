using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Идентификатор устройства: детерминированность, нормализация и живой провайдер Win/Linux.</summary>
public sealed class DeviceFingerprintTests
{
    [Fact]
    public void Create_IsStableForSameComponents()
    {
        DeviceFingerprint first = DeviceFingerprint.Create(
        [
            new DeviceFingerprintComponent("machine-id", "abc"),
            new DeviceFingerprintComponent("os", "linux"),
        ]);

        DeviceFingerprint second = DeviceFingerprint.Create(
        [
            new DeviceFingerprintComponent("os", "LINUX"),
            new DeviceFingerprintComponent("machine-id", "  ABC  "),
        ]);

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.BindingHash, second.BindingHash);
    }

    [Fact]
    public void Create_DifferentComponentsProduceDifferentHashes()
    {
        DeviceFingerprint first = DeviceFingerprint.Create(
            [new DeviceFingerprintComponent("machine-id", "abc")]);
        DeviceFingerprint second = DeviceFingerprint.Create(
            [new DeviceFingerprintComponent("machine-id", "abd")]);

        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void BindingHash_IsHalfOfSha256InHex()
    {
        DeviceFingerprint fingerprint = DeviceFingerprint.Create(
            [new DeviceFingerprintComponent("machine-id", "abc")]);

        Assert.Equal(64, fingerprint.Hash.Length);
        Assert.Equal(32, fingerprint.BindingHash.Length);
        Assert.Equal(fingerprint.Hash[..32], fingerprint.BindingHash);
        Assert.Equal(16, DeviceFingerprint.BindingHashSize);
    }

    [Fact]
    public void Create_RejectsEmptyComponents()
    {
        Assert.Throws<ArgumentException>(() => DeviceFingerprint.Create([]));
    }

    [Fact]
    public void BindingHashHelpers_RoundTrip()
    {
        DeviceFingerprint fingerprint = DeviceFingerprint.Create(
            [new DeviceFingerprintComponent("machine-id", "abc")]);

        byte[] bytes = DeviceFingerprint.BindingHashToBytes(fingerprint.BindingHash);

        Assert.Equal(16, bytes.Length);
        Assert.Equal(fingerprint.BindingHash, DeviceFingerprint.BindingHashFromBytes(bytes));
    }

    [Fact]
    public void NormalizeBindingHash_RejectsWrongLength()
    {
        Assert.Throws<ArgumentException>(() => DeviceFingerprint.NormalizeBindingHash("ABCD"));
    }

    [Fact]
    public void DefaultProvider_ReturnsStableNonEmptyFingerprint()
    {
        var provider = new DefaultDeviceFingerprintProvider();

        DeviceFingerprint first = provider.GetFingerprint();
        DeviceFingerprint second = provider.GetFingerprint();

        Assert.NotEmpty(first.Components);
        Assert.Contains(first.Components, component => component.Name == "os");
        Assert.Equal(first.BindingHash, second.BindingHash);
        Assert.Equal(32, first.BindingHash.Length);
    }
}
