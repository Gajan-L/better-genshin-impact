using System.IO;

namespace BetterGenshinImpact.Modules.MultiAccount;

public class GameLaunchContext
{
    public string ProfileId { get; init; } = string.Empty;

    public string ProfileName { get; init; } = string.Empty;

    public GameRegion Region { get; init; }

    public string InstallPath { get; init; } = string.Empty;

    public string LaunchArgs { get; init; } = string.Empty;

    public bool AutoEnterGame { get; init; } = true;

    public string GetProcessName()
    {
        return Path.GetFileNameWithoutExtension(InstallPath);
    }
}
