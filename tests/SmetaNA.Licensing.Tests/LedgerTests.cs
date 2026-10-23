using Xunit;

namespace SmetaNA.Licensing.Tests;

/// <summary>Журнал активаций: скользящее окно, отзыв, сериализация.</summary>
public sealed class LedgerTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CountActivations_UsesRollingWindow()
    {
        var time = new TestTimeProvider(Start);
        var ledger = new ActivationLedger(time);
        Guid keyId = Guid.NewGuid();

        ledger.AddActivation(keyId, Guid.NewGuid(), "AAAA1111AAAA1111AAAA1111AAAA1111", Start.AddDays(-370));
        ledger.AddActivation(keyId, Guid.NewGuid(), "BBBB2222BBBB2222BBBB2222BBBB2222", Start.AddDays(-10));
        ledger.AddActivation(Guid.NewGuid(), Guid.NewGuid(), "CCCC3333CCCC3333CCCC3333CCCC3333", Start.AddDays(-1));

        Assert.Equal(1, ledger.CountActivations(keyId));
    }

    [Fact]
    public void Revoke_MarksRecordAndListsToken()
    {
        var time = new TestTimeProvider(Start);
        var ledger = new ActivationLedger(time);
        Guid keyId = Guid.NewGuid();
        Guid tokenId = Guid.NewGuid();
        ledger.AddActivation(keyId, tokenId, "AAAA1111AAAA1111AAAA1111AAAA1111", Start);

        Assert.False(ledger.IsRevoked(tokenId));
        Assert.True(ledger.Revoke(tokenId, "перенос"));
        Assert.True(ledger.IsRevoked(tokenId));
        Assert.Equal(new[] { tokenId }, ledger.GetRevokedTokenIds());
        Assert.Equal("перенос", ledger.GetActivations(keyId)[0].RevocationReason);
    }

    [Fact]
    public void Revoke_UnknownToken_ReturnsFalse()
    {
        var time = new TestTimeProvider(Start);
        var ledger = new ActivationLedger(time);

        Assert.False(ledger.Revoke(Guid.NewGuid()));
    }

    [Fact]
    public void JsonRoundTrip_PreservesRecords()
    {
        var time = new TestTimeProvider(Start);
        var ledger = new ActivationLedger(time);
        Guid keyId = Guid.NewGuid();
        Guid tokenId = Guid.NewGuid();
        ledger.AddActivation(keyId, tokenId, "AAAA1111AAAA1111AAAA1111AAAA1111", Start);
        ledger.Revoke(tokenId, "возврат");

        ActivationLedger restored = ActivationLedger.FromJson(ledger.ToJson(), time);

        Assert.Equal(1, restored.CountActivations(keyId));
        Assert.True(restored.IsRevoked(tokenId));
        Assert.Equal(keyId, restored.Records[0].KeyId);
    }
}
