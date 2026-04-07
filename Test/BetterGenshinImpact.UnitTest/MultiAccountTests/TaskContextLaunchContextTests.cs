using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Modules.MultiAccount;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class TaskContextLaunchContextTests
{
    [Fact]
    public void RuntimeLaunchContext_TakesPrecedenceOverSavedConfig()
    {
        using var _ = new ConfigScope(new AllConfig
        {
            GenshinStartConfig = new GenshinStartConfig
            {
                InstallPath = @"D:\Games\YuanShen.exe",
                GenshinStartArgs = "--saved",
                AutoEnterGameEnabled = false,
            },
        });

        var taskContext = TaskContext.Instance();
        taskContext.ClearRuntimeLaunchContext();
        taskContext.SetRuntimeLaunchContext(new GameLaunchContext
        {
            ProfileId = "account-one",
            ProfileName = "Account One",
            Region = GameRegion.Global,
            InstallPath = @"D:\Games\GenshinImpact.exe",
            LaunchArgs = "--runtime",
            AutoEnterGame = true,
        });

        Assert.Equal(@"D:\Games\GenshinImpact.exe", taskContext.ResolveGenshinInstallPath());
        Assert.Equal("--runtime", taskContext.ResolveGenshinStartArgs());
        Assert.Equal(GameRegion.Global, taskContext.ResolveGameRegion());
        Assert.True(taskContext.ShouldAutoEnterGame());
        Assert.Equal("GenshinImpact", taskContext.GetResolvedGenshinGameProcessNameList().First());
    }

    [Fact]
    public void RuntimeLaunchContext_CanDisableAutoEnterGame()
    {
        using var _ = new ConfigScope(new AllConfig
        {
            GenshinStartConfig = new GenshinStartConfig
            {
                InstallPath = @"D:\Games\YuanShen.exe",
                GenshinStartArgs = "--saved",
                AutoEnterGameEnabled = true,
            },
        });

        var taskContext = TaskContext.Instance();
        taskContext.ClearRuntimeLaunchContext();
        taskContext.SetRuntimeLaunchContext(new GameLaunchContext
        {
            ProfileId = "account-two",
            ProfileName = "Account Two",
            Region = GameRegion.CNOfficial,
            InstallPath = @"D:\Games\YuanShen.exe",
            LaunchArgs = "--runtime",
            AutoEnterGame = false,
        });

        Assert.False(taskContext.ShouldAutoEnterGame());
    }

    [Fact]
    public void WithoutRuntimeLaunchContext_FallsBackToSavedConfig()
    {
        using var _ = new ConfigScope(new AllConfig
        {
            GenshinStartConfig = new GenshinStartConfig
            {
                InstallPath = @"D:\Games\YuanShen.exe",
                GenshinStartArgs = "--saved",
                AutoEnterGameEnabled = true,
            },
        });

        var taskContext = TaskContext.Instance();
        taskContext.ClearRuntimeLaunchContext();

        Assert.Equal(@"D:\Games\YuanShen.exe", taskContext.ResolveGenshinInstallPath());
        Assert.Equal("--saved", taskContext.ResolveGenshinStartArgs());
        Assert.Null(taskContext.ResolveGameRegion());
        Assert.True(taskContext.ShouldAutoEnterGame());
        Assert.Contains("YuanShen", taskContext.GetResolvedGenshinGameProcessNameList());
    }
}
