namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public interface IGameLoginFlowFactory
{
    IGameLoginFlow Create(GameRegion region);
}
