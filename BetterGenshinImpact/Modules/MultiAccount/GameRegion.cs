namespace BetterGenshinImpact.Modules.MultiAccount;

public enum GameRegion
{
    CNOfficial,
    Global,
}

public static class GameRegionExtensions
{
    public static bool SupportsRememberedAccountUiSwitch(this GameRegion region)
    {
        return region is GameRegion.CNOfficial or GameRegion.Global;
    }

    public static string GetGameServerCode(this GameRegion region)
    {
        return region switch
        {
            GameRegion.CNOfficial => "hk4e_cn",
            GameRegion.Global => "hk4e_global",
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };
    }

    public static string GetDefaultExecutableName(this GameRegion region)
    {
        return region switch
        {
            GameRegion.CNOfficial => "YuanShen.exe",
            GameRegion.Global => "GenshinImpact.exe",
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };
    }
}
