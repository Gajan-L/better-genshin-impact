using BetterGenshinImpact.Helpers;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class CommandLineOptionsTests
{
    [Fact]
    public void Parse_StartMultiAccountWithoutProfileNames_UsesEnabledProfiles()
    {
        var options = CommandLineOptions.Parse(["BetterGI.exe", "--startMultiAccount"]);

        Assert.Equal(CommandLineAction.StartMultiAccount, options.Action);
        Assert.Empty(options.MultiAccountProfileNames);
        Assert.True(options.ShouldDeferGameStart);
    }

    [Fact]
    public void Parse_StartMultiAccountWithProfileNames_CapturesNames()
    {
        var options = CommandLineOptions.Parse(["BetterGI.exe", "--startMultiAccount", "Account 1", "Account 2"]);

        Assert.Equal(CommandLineAction.StartMultiAccount, options.Action);
        Assert.Equal(["Account 1", "Account 2"], options.MultiAccountProfileNames);
    }

    [Fact]
    public void Parse_ListMultiAccountProfiles_UsesListAction()
    {
        var options = CommandLineOptions.Parse(["BetterGI.exe", "--listMultiAccountProfiles"]);

        Assert.Equal(CommandLineAction.ListMultiAccountProfiles, options.Action);
        Assert.True(options.HasTaskArgs);
        Assert.False(options.ShouldDeferGameStart);
    }

    [Fact]
    public void Parse_BareStartMultiAccountAlias_IsSupported()
    {
        var options = CommandLineOptions.Parse(["BetterGI.exe", "startMultiAccount", "Account 1"]);

        Assert.Equal(CommandLineAction.StartMultiAccount, options.Action);
        Assert.Equal(["Account 1"], options.MultiAccountProfileNames);
    }
}
