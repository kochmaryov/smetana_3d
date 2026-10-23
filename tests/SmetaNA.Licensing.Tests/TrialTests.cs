using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Пробный период с честным истечением: старт, срок, откат часов, повторный старт.</summary>
public sealed class TrialTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static LicenseService CreateService(
        VendorFixture vendor,
        TestTimeProvider time,
        TempStateDirectory dir,
        TrialOptions? options = null) =>
        new(vendor.PublicKeyPem, dir.Path, new FakeDeviceProvider("machine-a"), time, options);

    [Fact]
    public void Trial_StartsAndStaysActiveWithinPeriod()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(vendor, time, dir);

        Assert.Equal(LicenseState.NotLicensed, service.GetStatus().State);

        TrialStatus started = service.StartTrial();
        Assert.Equal(TrialStatusKind.Active, started.Kind);
        Assert.Equal(Start.AddDays(14), started.ExpiresAt);

        time.Advance(TimeSpan.FromDays(13));
        Assert.Equal(LicenseState.TrialActive, service.GetStatus().State);
    }

    [Fact]
    public void Trial_ExpiresHonestlyAfterPeriod()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(vendor, time, dir);
        service.StartTrial();

        time.Advance(TimeSpan.FromDays(14).Add(TimeSpan.FromMinutes(1)));

        Assert.Equal(TrialStatusKind.Expired, service.GetTrialStatus().Kind);
        Assert.Equal(LicenseState.TrialExpired, service.GetStatus().State);
    }

    [Fact]
    public void Trial_ClockRollback_DoesNotExtendPeriod()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(vendor, time, dir);
        service.StartTrial();

        time.Advance(TimeSpan.FromDays(15));
        Assert.Equal(LicenseState.TrialExpired, service.GetStatus().State);

        // Пользователь откатывает часы на месяц назад — триал не возобновляется.
        time.Advance(TimeSpan.FromDays(-45));

        LicenseStatus status = service.GetStatus();
        Assert.Equal(LicenseState.TrialExpired, status.State);
        Assert.True(status.ClockRollbackDetected);
    }

    [Fact]
    public void Trial_ClockRollback_IsFlaggedWhileStillActive()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(vendor, time, dir);
        service.StartTrial();

        time.Advance(TimeSpan.FromDays(5));
        service.GetStatus();
        time.Advance(TimeSpan.FromDays(-10));

        LicenseStatus status = service.GetStatus();

        // Откат виден, но «эффективное время» не откатывается: триал идёт по прежнему графику.
        Assert.Equal(LicenseState.TrialActive, status.State);
        Assert.True(status.ClockRollbackDetected);
    }

    [Fact]
    public void Trial_RepeatedStart_DoesNotExtendPeriod()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(vendor, time, dir);
        TrialStatus first = service.StartTrial();

        time.Advance(TimeSpan.FromDays(10));
        TrialStatus second = service.StartTrial();

        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(Start.AddDays(14), second.ExpiresAt);
    }

    [Fact]
    public void Trial_UsesConfiguredLength()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        using LicenseService service = CreateService(
            vendor, time, dir, new TrialOptions { Length = TimeSpan.FromDays(7) });

        TrialStatus started = service.StartTrial();

        Assert.Equal(Start.AddDays(7), started.ExpiresAt);
    }

    [Fact]
    public void Trial_IsPerDevice()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dirA = new TempStateDirectory();
        using var dirB = new TempStateDirectory();
        using LicenseService serviceA = CreateService(vendor, time, dirA);
        serviceA.StartTrial();

        foreach (string file in Directory.GetFiles(dirA.Path))
        {
            File.Copy(file, Path.Combine(dirB.Path, Path.GetFileName(file)), overwrite: true);
        }

        using var serviceB = new LicenseService(
            vendor.PublicKeyPem, dirB.Path, new FakeDeviceProvider("machine-b"), time);

        Assert.Equal(LicenseState.InvalidState, serviceB.GetStatus().State);
    }

    [Fact]
    public void ExpiredTrial_ReplacedByActivation()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var dir = new TempStateDirectory();
        var device = new FakeDeviceProvider("machine-a");
        using LicenseService service = CreateService(vendor, time, dir);
        service.StartTrial();

        time.Advance(TimeSpan.FromDays(30));
        Assert.Equal(LicenseState.TrialExpired, service.GetStatus().State);

        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, device.GetFingerprint().BindingHash);
        Assert.True(service.ApplyActivationToken(token.ToTokenString(), key).Success);

        Assert.Equal(LicenseState.Licensed, service.GetStatus(key).State);
    }
}
