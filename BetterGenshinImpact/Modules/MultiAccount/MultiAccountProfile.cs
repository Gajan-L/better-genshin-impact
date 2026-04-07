using CommunityToolkit.Mvvm.ComponentModel;

namespace BetterGenshinImpact.Modules.MultiAccount;

public partial class MultiAccountProfile : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "New Account";

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private GameRegion _region = GameRegion.CNOfficial;

    [ObservableProperty]
    private string _installPath = string.Empty;

    [ObservableProperty]
    private string _launchArgs = string.Empty;

    [ObservableProperty]
    private string _oneDragonConfigName = string.Empty;

    [ObservableProperty]
    private string _rememberedAccountLabel = string.Empty;

    public string RegionDisplayKey => Region switch
    {
        GameRegion.CNOfficial => "国服官服",
        GameRegion.Global => "国际服",
        _ => Region.ToString(),
    };

    partial void OnRegionChanged(GameRegion value)
    {
        OnPropertyChanged(nameof(RegionDisplayKey));
    }

    public GameLaunchContext ToLaunchContext(bool autoEnterGame = true)
    {
        return new GameLaunchContext
        {
            ProfileId = Id,
            ProfileName = Name,
            Region = Region,
            InstallPath = InstallPath,
            LaunchArgs = LaunchArgs,
            AutoEnterGame = autoEnterGame,
        };
    }
}
