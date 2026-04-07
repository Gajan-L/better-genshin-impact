using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class GameExecutablePathResolverTests
{
    [Fact]
    public void Find_PrefersExistingConfiguredCnPath()
    {
        using var tempDirectory = new TemporaryDirectory();
        var configuredPath = Path.Combine(tempDirectory.Path, "Configured", "miHoYo Launcher", "games", "Genshin Impact Game", "YuanShen.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(configuredPath)!);
        File.WriteAllText(configuredPath, string.Empty);

        var resolver = new GameExecutablePathResolver([tempDirectory.Path]);

        var path = resolver.Find(GameRegion.CNOfficial, configuredPath);

        Assert.Equal(configuredPath, path);
    }

    [Fact]
    public void Find_DetectsCnLauncherPathUnderGamesDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var searchRoot = Path.Combine(tempDirectory.Path, "D");
        var executablePath = Path.Combine(searchRoot, "Games", "miHoYo Launcher", "games", "Genshin Impact Game", "YuanShen.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        File.WriteAllText(executablePath, string.Empty);

        var resolver = new GameExecutablePathResolver([searchRoot, Path.Combine(searchRoot, "Games")]);

        var path = resolver.Find(GameRegion.CNOfficial);

        Assert.Equal(executablePath, path);
    }

    [Fact]
    public void Find_DetectsGlobalLauncherPathUnderGamesDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var searchRoot = Path.Combine(tempDirectory.Path, "D");
        var executablePath = Path.Combine(searchRoot, "Games", "HoYoPlay", "games", "Genshin Impact game", "GenshinImpact.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        File.WriteAllText(executablePath, string.Empty);

        var resolver = new GameExecutablePathResolver([searchRoot, Path.Combine(searchRoot, "Games")]);

        var path = resolver.Find(GameRegion.Global);

        Assert.Equal(executablePath, path);
    }

    [Fact]
    public void Find_DetectsCnLauncherPathUnderLowercaseGamesDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var searchRoot = Path.Combine(tempDirectory.Path, "D");
        var executablePath = Path.Combine(searchRoot, "games", "miHoYo Launcher", "games", "Genshin Impact Game", "YuanShen.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        File.WriteAllText(executablePath, string.Empty);

        var resolver = new GameExecutablePathResolver([searchRoot, Path.Combine(searchRoot, "games")]);

        var path = resolver.Find(GameRegion.CNOfficial);

        Assert.Equal(executablePath, path);
    }

    [Fact]
    public void Find_DetectsGlobalLauncherPathUnderProgramFilesDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var searchRoot = Path.Combine(tempDirectory.Path, "E");
        var executablePath = Path.Combine(searchRoot, "Program Files", "HoYoPlay", "games", "Genshin Impact game", "GenshinImpact.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        File.WriteAllText(executablePath, string.Empty);

        var resolver = new GameExecutablePathResolver([searchRoot, Path.Combine(searchRoot, "Program Files")]);

        var path = resolver.Find(GameRegion.Global);

        Assert.Equal(executablePath, path);
    }
}
