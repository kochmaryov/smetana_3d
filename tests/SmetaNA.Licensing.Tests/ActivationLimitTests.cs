using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Лимит активаций в год: скользящее окно 365 дней, отказ сверх лимита, сохранение квоты после отзыва.</summary>
public sealed class ActivationLimitTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnnualLimit_BlocksActivationAboveQuota()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey(maxActivationsPerYear: 2);

        ActivationIssueResult first = vendor.Issue(key, "AAAA1111AAAA1111AAAA1111AAAA1111");
        ActivationIssueResult second = vendor.Issue(key, "BBBB2222BBBB2222BBBB2222BBBB2222");
        ActivationIssueResult third = vendor.Issue(key, "CCCC3333CCCC3333CCCC3333CCCC3333");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(third.Success);
        Assert.Equal(IssueProblem.AnnualLimitExceeded, third.Problem);
        Assert.Equal(2, vendor.Ledger.CountActivations(key.Data.KeyId));
    }

    [Fact]
    public void IssueNumbersActivationsSequentially()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey(maxActivationsPerYear: 3);

        ActivationToken first = vendor.IssueToken(key, "AAAA1111AAAA1111AAAA1111AAAA1111");
        ActivationToken second = vendor.IssueToken(key, "BBBB2222BBBB2222BBBB2222BBBB2222");

        Assert.Equal(1, first.ActivationOrdinal);
        Assert.Equal(2, second.ActivationOrdinal);
    }

    [Fact]
    public void AnnualLimit_ResetsAfterRollingWindow()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey(maxActivationsPerYear: 1);

        Assert.True(vendor.Issue(key, "AAAA1111AAAA1111AAAA1111AAAA1111").Success);
        Assert.False(vendor.Issue(key, "BBBB2222BBBB2222BBBB2222BBBB2222").Success);

        time.Advance(TimeSpan.FromDays(366));

        Assert.True(vendor.Issue(key, "CCCC3333CCCC3333CCCC3333CCCC3333").Success);
    }

    [Fact]
    public void RevokedActivation_DoesNotReturnAnnualQuota()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey(maxActivationsPerYear: 1);
        ActivationToken token = vendor.IssueToken(key, "AAAA1111AAAA1111AAAA1111AAAA1111");

        Assert.True(vendor.Issuer.Revoke(token.TokenId));
        ActivationIssueResult again = vendor.Issue(key, "BBBB2222BBBB2222BBBB2222BBBB2222");

        // Политика Plasticity-класса: отзыв освобождает устройство, но не возвращает годовую квоту;
        // перенос лимита выполняет поддержка вручную.
        Assert.False(again.Success);
        Assert.Equal(IssueProblem.AnnualLimitExceeded, again.Problem);
    }

    [Fact]
    public void Issue_RejectsExpiredKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey(
            kind: LicenseKind.Subscription,
            expiresAt: Start.AddDays(30));

        time.Advance(TimeSpan.FromDays(31));
        ActivationIssueResult result = vendor.Issue(key, "AAAA1111AAAA1111AAAA1111AAAA1111");

        Assert.False(result.Success);
        Assert.Equal(IssueProblem.ExpiredKey, result.Problem);
    }

    [Fact]
    public void Issue_RejectsRequestForAnotherKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();
        LicenseKey otherKey = vendor.CreateKey();

        ActivationRequest foreignRequest = ActivationRequest.Create(
            otherKey.Data.KeyId, "AAAA1111AAAA1111AAAA1111AAAA1111", Start);
        ActivationIssueResult result = vendor.Issuer.Issue(key, foreignRequest);

        Assert.False(result.Success);
        Assert.Equal(IssueProblem.KeyMismatch, result.Problem);
    }

    [Fact]
    public void Issue_RejectsForgedKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        LicenseKey foreignKey = impostor.CreateKey();

        ActivationIssueResult result = vendor.Issue(foreignKey, "AAAA1111AAAA1111AAAA1111AAAA1111");

        Assert.False(result.Success);
        Assert.Equal(IssueProblem.InvalidKey, result.Problem);
    }
}
