namespace BetterGenshinImpact.Modules.MultiAccount;

public interface IGameSessionController
{
    Task EnsureGameClosedAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default);

    Task CloseGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default);
}
