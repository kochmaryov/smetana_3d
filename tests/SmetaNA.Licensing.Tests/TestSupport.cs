using System.Security.Cryptography;

namespace SmetaNA.Licensing.Tests;

/// <summary>Управляемые часы для детерминированных тестов.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    public TestTimeProvider(DateTimeOffset start) => UtcNow = start;

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan delta) => UtcNow += delta;
}

/// <summary>Фиктивное устройство: идентификатор строится из метки.</summary>
internal sealed class FakeDeviceProvider : IDeviceFingerprintProvider
{
    private readonly string _machineId;

    public FakeDeviceProvider(string machineId) => _machineId = machineId;

    public DeviceFingerprint GetFingerprint() =>
        DeviceFingerprint.Create([new DeviceFingerprintComponent("machine-id", _machineId)]);
}

/// <summary>Временная папка локального состояния; удаляется после теста.</summary>
internal sealed class TempStateDirectory : IDisposable
{
    public TempStateDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "smetana-lic-tests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Контур продавца: пара ключей, генератор ключей, журнал и инструмент выдачи токенов.</summary>
internal sealed class VendorFixture : IDisposable
{
    private readonly TestTimeProvider _time;

    public VendorFixture(TestTimeProvider time)
    {
        _time = time;
        SigningKey = LicenseKeyGenerator.CreateSigningKey();
        Generator = new LicenseKeyGenerator(SigningKey);
        PublicKeyPem = Generator.ExportPublicKeyPem();
        Ledger = new ActivationLedger(time);
        Issuer = new ActivationTokenIssuer(SigningKey, Ledger, time);
    }

    public ECDsa SigningKey { get; }

    public LicenseKeyGenerator Generator { get; }

    public string PublicKeyPem { get; }

    public ActivationLedger Ledger { get; }

    public ActivationTokenIssuer Issuer { get; }

    public LicenseKey CreateKey(
        int maxActivationsPerYear = 4,
        LicenseKind kind = LicenseKind.Perpetual,
        DateTimeOffset? expiresAt = null,
        string licensee = "user@example.com") =>
        Generator.Generate(new LicenseKeyData(
            Guid.NewGuid(),
            kind,
            _time.GetUtcNow(),
            expiresAt,
            maxActivationsPerYear,
            licensee));

    public ActivationIssueResult Issue(LicenseKey key, string deviceFingerprintHash) =>
        Issuer.Issue(key, ActivationRequest.Create(key.Data.KeyId, deviceFingerprintHash, _time.GetUtcNow()));

    public ActivationToken IssueToken(LicenseKey key, string deviceFingerprintHash) =>
        Issue(key, deviceFingerprintHash).Token!;

    public void Dispose()
    {
        Issuer.Dispose();
        Generator.Dispose();
        SigningKey.Dispose();
    }
}
