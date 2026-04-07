using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class RememberedAccountMatcherTests
{
    [Theory]
    [InlineData("17512345625", "175******25")]
    [InlineData("175******25", "17512345625")]
    [InlineData("abc123@mail.com", "abc***@mail.com")]
    [InlineData("abc***@mail.com", "abc123@mail.com")]
    public void Matches_ReturnsTrueForFullAndMaskedVariants(string targetAccountText, string candidateText)
    {
        Assert.True(RememberedAccountMatcher.Matches(targetAccountText, candidateText));
    }

    [Fact]
    public void Matches_ReturnsFalseForDifferentAccounts()
    {
        Assert.False(RememberedAccountMatcher.Matches("17512345625", "176******25"));
    }

    [Fact]
    public void Normalize_RewritesCommonMaskedGlyphsToAsterisks()
    {
        var normalized = RememberedAccountMatcher.Normalize(" 175\u00A0\u2022\u25CF\u25E6\u00B7\u2219\u2027 25 ");

        Assert.Equal("175******25", normalized);
    }
}
