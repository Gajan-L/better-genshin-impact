using BetterGenshinImpact.Service;

namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public interface IGameLoginFlow
{
    GameRegion Region { get; }

    Task EnterGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default);
}
