using Xunit;

namespace SmetaNA.Licensing.Tests;

public sealed class CrockfordBase32Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(37)]
    [InlineData(64)]
    public void EncodeDecode_RoundTrips(int length)
    {
        byte[] data = Enumerable.Range(0, length).Select(i => (byte)(i * 7 + 1)).ToArray();

        string encoded = CrockfordBase32.Encode(data);
        byte[] decoded = CrockfordBase32.Decode(encoded);

        Assert.Equal(data, decoded);
    }

    [Fact]
    public void Decode_AcceptsGroupingLowercaseAndAliases()
    {
        byte[] data = [0x00, 0xFF, 0x10];
        string encoded = CrockfordBase32.Encode(data);

        // O/0, I/L/1 — взаимозаменяемые символы; регистр и дефисы игнорируются.
        string noisy = CrockfordBase32.Group(encoded).ToLowerInvariant()
            .Replace('0', 'O')
            .Replace('1', 'l');

        Assert.Equal(data, CrockfordBase32.Decode(noisy));
    }

    [Fact]
    public void Decode_RejectsInvalidCharacter()
    {
        Assert.Throws<LicenseFormatException>(() => CrockfordBase32.Decode("ABC$DEF"));
    }

    [Fact]
    public void Group_InsertsDashEveryFiveCharacters()
    {
        Assert.Equal("ABCDE-FGHIJ-KL", CrockfordBase32.Group("ABCDEFGHIJKL"));
        Assert.Equal("ABCDE", CrockfordBase32.Group("ABCDE"));
    }

    [Fact]
    public void Ungroup_RemovesSeparators()
    {
        Assert.Equal("ABCDEFGHIJKL", CrockfordBase32.Ungroup("ABCDE-FGHIJ-KL"));
    }
}
