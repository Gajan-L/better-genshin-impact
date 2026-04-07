namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public class GameLoginFlowFactory(RememberedAccountUiLoginAutomation rememberedAccountUiLoginAutomation) : IGameLoginFlowFactory
{
    public IGameLoginFlow Create(GameRegion region)
    {
        return region switch
        {
            GameRegion.CNOfficial => new CnOfficialLoginFlow(rememberedAccountUiLoginAutomation),
            GameRegion.Global => new GlobalLoginFlow(rememberedAccountUiLoginAutomation),
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };
    }
}
