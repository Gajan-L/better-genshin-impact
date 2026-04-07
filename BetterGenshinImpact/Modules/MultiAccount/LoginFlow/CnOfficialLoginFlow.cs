namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public class CnOfficialLoginFlow(RememberedAccountUiLoginAutomation rememberedAccountUiLoginAutomation) : IGameLoginFlow
{
    public GameRegion Region => GameRegion.CNOfficial;

    public Task EnterGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        return rememberedAccountUiLoginAutomation.EnterGameAsync(profile, cancellationToken);
    }
}
