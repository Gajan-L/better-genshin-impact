using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class RememberedAccountChooserLayoutTests
{
    [Fact]
    public void IsExpanded_ReturnsFalseForCollapsedChooser()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("136******52", 780, 520, 180, 40),
            };

        Assert.False(RememberedAccountChooserLayout.IsExpanded(entries));
    }

    [Fact]
    public void IsExpanded_ReturnsTrueWhenDropdownShowsMultipleAccounts()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("136******52", 780, 520, 180, 40),
                new RememberedAccountEntry("136******52", 780, 610, 180, 40),
                new RememberedAccountEntry("175******25", 780, 700, 180, 40),
            };

        Assert.True(RememberedAccountChooserLayout.IsExpanded(entries));
    }

    [Fact]
    public void TryGetChooserAnchor_ReturnsTopMostMaskedAccount()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("175******25", 780, 700, 180, 40),
                new RememberedAccountEntry("136******52", 780, 520, 180, 40),
                new RememberedAccountEntry("136******52", 780, 610, 180, 40),
            };

        var anchor = RememberedAccountChooserLayout.TryGetChooserAnchor(entries);

        Assert.Equal(new RememberedAccountEntry("136******52", 780, 520, 180, 40), anchor);
    }

    [Fact]
    public void TryPickTargetEntry_PrefersExpandedOptionOverCollapsedHeader()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("136******52", 780, 520, 180, 40),
                new RememberedAccountEntry("136******52", 780, 610, 180, 40),
                new RememberedAccountEntry("175******25", 780, 700, 180, 40),
            };

        var selected = RememberedAccountChooserLayout.TryPickTargetEntry(entries, "13612345652");

        Assert.Equal(new RememberedAccountEntry("136******52", 780, 610, 180, 40), selected);
    }

    [Fact]
    public void TryPickTargetEntry_MatchesFullAccountAgainstMaskedOption()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("136******52", 780, 520, 180, 40),
                new RememberedAccountEntry("136******52", 780, 610, 180, 40),
                new RememberedAccountEntry("175******25", 780, 700, 180, 40),
            };

        var selected = RememberedAccountChooserLayout.TryPickTargetEntry(entries, "17512345625");

        Assert.Equal(new RememberedAccountEntry("175******25", 780, 700, 180, 40), selected);
    }

    [Fact]
    public void IsCurrentAccountMatch_ReturnsTrueForCollapsedChooserWhenVisibleAccountMatches()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("ho****es@gmail.com", 780, 520, 240, 40),
            };

        Assert.True(RememberedAccountChooserLayout.IsCurrentAccountMatch(entries, "ho12345es@gmail.com"));
    }

    [Fact]
    public void IsCurrentAccountMatch_ReturnsFalseWhenVisibleAccountDoesNotMatch()
    {
        var entries =
            new[]
            {
                new RememberedAccountEntry("ho****es@gmail.com", 780, 520, 240, 40),
            };

        Assert.False(RememberedAccountChooserLayout.IsCurrentAccountMatch(entries, "other-test@gmail.com"));
    }
}
