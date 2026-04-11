using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class MultiAccountCommandLineProfileSelectorTests
{
    [Fact]
    public void SelectProfiles_WithoutRequestedNames_UsesEnabledProfilesInSavedOrder()
    {
        var profiles = new[]
        {
            CreateProfile("account-2", "Account 2", order: 1, isEnabled: true),
            CreateProfile("account-1", "Account 1", order: 0, isEnabled: true),
            CreateProfile("account-3", "Account 3", order: 2, isEnabled: false),
        };

        var selectedProfiles = MultiAccountCommandLineProfileSelector.SelectProfiles(profiles);

        Assert.Equal(["account-1", "account-2"], selectedProfiles.Select(profile => profile.Id));
    }

    [Fact]
    public void SelectProfiles_WithRequestedNames_PreservesCliOrderAndIncludesDisabledProfiles()
    {
        var profiles = new[]
        {
            CreateProfile("account-1", "Account 1", order: 0, isEnabled: true),
            CreateProfile("account-2", "Account 2", order: 1, isEnabled: false),
            CreateProfile("account-3", "Account 3", order: 2, isEnabled: true),
        };

        var selectedProfiles = MultiAccountCommandLineProfileSelector.SelectProfiles(
            profiles,
            ["Account 2", "Account 1"]);

        Assert.Equal(["account-2", "account-1"], selectedProfiles.Select(profile => profile.Id));
    }

    [Fact]
    public void SelectProfiles_WithRequestedIds_DeduplicatesMatches()
    {
        var profiles = new[]
        {
            CreateProfile("account-1", "Account 1", order: 0, isEnabled: true),
        };

        var selectedProfiles = MultiAccountCommandLineProfileSelector.SelectProfiles(
            profiles,
            ["account-1", "Account 1"]);

        Assert.Single(selectedProfiles);
        Assert.Equal("account-1", selectedProfiles[0].Id);
    }

    [Fact]
    public void SelectProfiles_WithMissingName_ThrowsHelpfulMessage()
    {
        var profiles = new[]
        {
            CreateProfile("account-1", "Account 1", order: 0, isEnabled: true),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MultiAccountCommandLineProfileSelector.SelectProfiles(profiles, ["Missing Account"]));

        Assert.Contains("Multi-account profile does not exist", exception.Message);
        Assert.Contains("--listMultiAccountProfiles", exception.Message);
    }

    [Fact]
    public void SelectProfiles_WithAmbiguousName_RequiresProfileId()
    {
        var profiles = new[]
        {
            CreateProfile("account-1", "Shared Name", order: 0, isEnabled: true),
            CreateProfile("account-2", "Shared Name", order: 1, isEnabled: true),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MultiAccountCommandLineProfileSelector.SelectProfiles(profiles, ["Shared Name"]));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MultiAccountProfile CreateProfile(string id, string name, int order, bool isEnabled)
    {
        return new MultiAccountProfile
        {
            Id = id,
            Name = name,
            Order = order,
            IsEnabled = isEnabled,
            Region = GameRegion.CNOfficial,
            InstallPath = $@"D:\Games\{id}.exe",
            OneDragonConfigName = "dragon-one",
            RememberedAccountLabel = "175******25",
        };
    }
}
