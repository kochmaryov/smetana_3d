using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Round-trip лицензионного ключа: данные и каноничная строка переживают сериализацию.</summary>
public sealed class LicenseKeyRoundTripTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GenerateThenParse_PreservesAllFields()
    {
        var time = new TestTimeProvider(IssuedAt);
        using var vendor = new VendorFixture(time);
        DateTimeOffset expiresAt = IssuedAt.AddYears(1);
        LicenseKey key = vendor.CreateKey(
            maxActivationsPerYear: 3,
            kind: LicenseKind.Subscription,
            expiresAt: expiresAt,
            licensee: "Иван Петров <ivan@example.com>");

        LicenseKey parsed = LicenseKey.Parse(key.ToLicenseString());

        Assert.Equal(key.Data.KeyId, parsed.Data.KeyId);
        Assert.Equal(LicenseKind.Subscription, parsed.Data.Kind);
        Assert.Equal(IssuedAt, parsed.Data.IssuedAt);
        Assert.Equal(expiresAt, parsed.Data.ExpiresAt);
        Assert.Equal(3, parsed.Data.MaxActivationsPerYear);
        Assert.Equal(key.Data.LicenseeHash, parsed.Data.LicenseeHash);
        Assert.Equal(key.Signature, parsed.Signature);
    }

    [Fact]
    public void Parse_OutputIsCanonicalAndStable()
    {
        var time = new TestTimeProvider(IssuedAt);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();
        string text = key.ToLicenseString();

        LicenseKey parsed = LicenseKey.Parse(text);

        Assert.StartsWith($"{LicenseKey.KeyPrefix}.", text);
        Assert.Equal(text, parsed.ToLicenseString());
        Assert.Equal(text, parsed.ToString());
    }

    [Fact]
    public void Parse_ToleratesCaseWhitespaceAndGrouping()
    {
        var time = new TestTimeProvider(IssuedAt);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();
        string text = key.ToLicenseString();

        string noisy = "  " + text.ToLowerInvariant().Replace(".", " . ") + "\r\n";
        LicenseKey parsed = LicenseKey.Parse(noisy);

        Assert.Equal(key.Data.KeyId, parsed.Data.KeyId);
    }

    [Fact]
    public void Parse_RejectsMalformedStrings()
    {
        Assert.Throws<LicenseFormatException>(() => LicenseKey.Parse("SMN1.AAAAA"));
        Assert.Throws<LicenseFormatException>(() => LicenseKey.Parse("SMN2.AAAA.BBBB"));
        Assert.Throws<LicenseFormatException>(() => LicenseKey.Parse("нечто"));
        Assert.Throws<ArgumentNullException>(() => LicenseKey.Parse(null!));

        Assert.False(LicenseKey.TryParse("SMN1.AAAAA", out LicenseKey? key));
        Assert.Null(key);
    }

    [Fact]
    public void PerpetualKey_HasNoExpiry()
    {
        var time = new TestTimeProvider(IssuedAt);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();

        LicenseKey parsed = LicenseKey.Parse(key.ToLicenseString());

        Assert.Equal(LicenseKind.Perpetual, parsed.Data.Kind);
        Assert.Null(parsed.Data.ExpiresAt);
    }

    [Fact]
    public void LicenseeHash_MatchesIgnoringCaseAndWhitespace()
    {
        var data = new LicenseKeyData(Guid.NewGuid(), LicenseKind.Perpetual, IssuedAt, null, 2, "ivan@example.com");

        Assert.True(data.MatchesLicensee("  IVAN@Example.com "));
        Assert.False(data.MatchesLicensee("petrov@example.com"));
        Assert.Equal(8, data.LicenseeHash.Length);
    }

    [Fact]
    public void KeyData_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => new LicenseKeyData(
            Guid.Empty, LicenseKind.Perpetual, IssuedAt, null, 2));

        Assert.Throws<ArgumentOutOfRangeException>(() => new LicenseKeyData(
            Guid.NewGuid(), LicenseKind.Perpetual, IssuedAt, null, 0));

        Assert.Throws<ArgumentException>(() => new LicenseKeyData(
            Guid.NewGuid(), LicenseKind.Perpetual, IssuedAt, IssuedAt.AddDays(-1), 2));

        Assert.Throws<ArgumentOutOfRangeException>(() => new LicenseKeyData(
            Guid.NewGuid(), LicenseKind.Perpetual, IssuedAt.AddYears(300), null, 2));
    }

    [Fact]
    public void Signature_HasP256Size()
    {
        var time = new TestTimeProvider(IssuedAt);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();

        Assert.Equal(64, key.Signature.Length);
    }
}
