namespace BetterGenshinImpact.Modules.MultiAccount.Runner;

public interface IOneDragonRunner
{
    Task RunAsync(string configName, bool ensureGameStarted = true, CancellationToken cancellationToken = default);
}
