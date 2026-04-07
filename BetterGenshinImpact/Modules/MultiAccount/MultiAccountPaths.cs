using BetterGenshinImpact.Core.Config;

namespace BetterGenshinImpact.Modules.MultiAccount;

public static class MultiAccountPaths
{
    public static string RootDirectory => Global.Absolute(@"User\MultiAccount");

    public static string ProfilesDirectory => Path.Combine(RootDirectory, "profiles");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");
}
