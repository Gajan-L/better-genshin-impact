using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class MultiAccountProfileStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsProfilesInOrder()
    {
        using var tempDirectory = new TemporaryDirectory();
        var store = new MultiAccountProfileStore(tempDirectory.Path);

        var laterProfile = new MultiAccountProfile
        {
            Id = "profile-b",
            Name = "Second",
            Order = 1,
            Region = GameRegion.Global,
            InstallPath = @"D:\Games\GenshinImpact.exe",
            LaunchArgs = "--global",
            OneDragonConfigName = "global-config",
            RememberedAccountLabel = "ab***@mail.com",
        };

        var earlierProfile = new MultiAccountProfile
        {
            Id = "profile-a",
            Name = "First",
            Order = 0,
            Region = GameRegion.CNOfficial,
            InstallPath = @"D:\Games\YuanShen.exe",
            LaunchArgs = "--cn",
            OneDragonConfigName = "cn-config",
            RememberedAccountLabel = "175******25",
        };

        await store.SaveAllAsync([laterProfile, earlierProfile]);

        var loadedProfiles = await store.LoadAsync();

        Assert.Collection(
            loadedProfiles,
            first =>
            {
                Assert.Equal("profile-a", first.Id);
                Assert.Equal("First", first.Name);
                Assert.Equal(GameRegion.CNOfficial, first.Region);
                Assert.Equal("cn-config", first.OneDragonConfigName);
                Assert.Equal("175******25", first.RememberedAccountLabel);
            },
            second =>
            {
                Assert.Equal("profile-b", second.Id);
                Assert.Equal("Second", second.Name);
                Assert.Equal(GameRegion.Global, second.Region);
                Assert.Equal("--global", second.LaunchArgs);
                Assert.Equal("ab***@mail.com", second.RememberedAccountLabel);
            });
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacyBilibiliRegionAndIgnoresLegacySnapshotFields()
    {
        using var tempDirectory = new TemporaryDirectory();
        var store = new MultiAccountProfileStore(tempDirectory.Path);
        var profilePath = Path.Combine(tempDirectory.Path, "legacy.json");

        await File.WriteAllTextAsync(
            profilePath,
            """
            {
              "id": "legacy",
              "name": "Legacy",
              "order": 0,
              "region": "CNBilibili",
              "switchMode": "LauncherSnapshot",
              "installPath": "D:\\Games\\miHoYo Launcher\\games\\Genshin Impact Game\\YuanShen.exe",
              "launchArgs": "",
              "oneDragonConfigName": "cn-config",
              "rememberedAccountLabel": "175******25",
              "lastCapturedAt": "2026-04-04T12:00:00+00:00"
            }
            """);

        var loadedProfiles = await store.LoadAsync();

        var profile = Assert.Single(loadedProfiles);
        Assert.Equal(GameRegion.CNOfficial, profile.Region);
        Assert.Equal("175******25", profile.RememberedAccountLabel);
    }
}
