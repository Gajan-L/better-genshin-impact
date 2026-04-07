namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public class GlobalLoginFlow(RememberedAccountUiLoginAutomation rememberedAccountUiLoginAutomation) : IGameLoginFlow
{
    public GameRegion Region => GameRegion.Global;

    public Task EnterGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        return rememberedAccountUiLoginAutomation.EnterGameAsync(profile, cancellationToken);
    }
}
