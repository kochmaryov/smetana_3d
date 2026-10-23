using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Список отзывов: создание продавцом, доставка офлайн, проверка подписи.</summary>
public sealed class RevocationListTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RevocationList_RoundTripsThroughEnvelope()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        LicenseKey key = vendor.CreateKey();
        ActivationToken token = vendor.IssueToken(key, "AAAA1111AAAA1111AAAA1111AAAA1111");
        vendor.Issuer.Revoke(token.TokenId, "возврат средств");
        RevocationList created = vendor.Issuer.CreateRevocationList();

        RevocationList parsed = RevocationList.Parse(created.ToEnvelopeString(), vendor.PublicKeyPem);

        Assert.True(parsed.IsRevoked(token.TokenId));
        Assert.Single(parsed.RevokedTokenIds);
        Assert.Equal(Start, parsed.GeneratedAt);
    }

    [Fact]
    public void RevocationList_RejectsWrongVendorKey()
    {
        var time = new TestTimeProvider(Start);
        using var vendor = new VendorFixture(time);
        using var impostor = new VendorFixture(time);
        RevocationList list = vendor.Issuer.CreateRevocationList();

        Assert.Throws<LicenseFormatException>(
            () => RevocationList.Parse(list.ToEnvelopeString(), impostor.PublicKeyPem));
    }

    [Fact]
    public void RevocationList_RejectsMalformedEnvelope()
    {
        Assert.Throws<LicenseFormatException>(() => RevocationList.Parse("{ не json", "не ключ"));
    }
}
