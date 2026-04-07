using System.Diagnostics;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.ViewModel.Pages;

namespace BetterGenshinImpact.Modules.MultiAccount;

public class GameSessionController : IGameSessionController
{
    private static readonly string[] LauncherProcessNames =
    [
        "HYP",
        "HYPHelper",
    ];

    public async Task EnsureGameClosedAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        App.GetService<HomePageViewModel>()?.StopDispatcher();
        SystemControl.CloseGame();
        CloseLauncherProcesses();
        await WaitForGameExitAsync(profile, cancellationToken);
        await WaitForLauncherExitAsync(cancellationToken);
    }

    public async Task CloseGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        SystemControl.CloseGame();
        CloseLauncherProcesses();
        await WaitForGameExitAsync(profile, cancellationToken);
        await WaitForLauncherExitAsync(cancellationToken);
    }

    private static async Task WaitForGameExitAsync(MultiAccountProfile profile, CancellationToken cancellationToken)
    {
        var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileNameWithoutExtension(profile.InstallPath),
            Path.GetFileNameWithoutExtension(profile.Region.GetDefaultExecutableName()),
            "YuanShen",
            "GenshinImpact",
            "Genshin Impact Cloud Game",
            "Genshin Impact Cloud",
        };

        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasRunningProcess = processNames.Any(processName => Process.GetProcessesByName(processName).Length > 0);
            if (!hasRunningProcess)
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for game process to exit for profile {profile.Name}.");
    }

    private static void CloseLauncherProcesses()
    {
        foreach (var processName in LauncherProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort. Wait loop below will surface a timeout if launcher remains open.
                }
            }
        }
    }

    private static async Task WaitForLauncherExitAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasRunningProcess = LauncherProcessNames.Any(processName => Process.GetProcessesByName(processName).Length > 0);
            if (!hasRunningProcess)
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for HYP launcher processes to exit.");
    }
}
