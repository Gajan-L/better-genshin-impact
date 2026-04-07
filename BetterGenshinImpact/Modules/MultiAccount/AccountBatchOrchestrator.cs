using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Modules.MultiAccount.LoginFlow;
using BetterGenshinImpact.Modules.MultiAccount.Runner;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.Modules.MultiAccount;

public class AccountBatchOrchestrator(
    IOneDragonRunner oneDragonRunner,
    IGameLoginFlowFactory gameLoginFlowFactory,
    IGameSessionController gameSessionController,
    OneDragonConfigCatalog oneDragonConfigCatalog,
    ILogger<AccountBatchOrchestrator> logger,
    string? logsDirectory = null)
{
    private readonly string _logsDirectory = logsDirectory ?? MultiAccountPaths.LogsDirectory;

    public async Task<IReadOnlyList<BatchRunRecord>> RunBatchAsync(
        IEnumerable<MultiAccountProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_logsDirectory);
        var logPath = Path.Combine(_logsDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jsonl");
        var records = new List<BatchRunRecord>();

        try
        {
            foreach (var profile in profiles.OrderBy(profile => profile.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startedAt = DateTimeOffset.UtcNow;
                BatchRunRecord record;
                try
                {
                    ValidateProfile(profile);

                    await gameSessionController.EnsureGameClosedAsync(profile, cancellationToken);

                    TaskContext.Instance().SetRuntimeLaunchContext(profile.ToLaunchContext());
                    var loginFlow = gameLoginFlowFactory.Create(profile.Region);
                    await loginFlow.EnterGameAsync(profile, cancellationToken);
                    await oneDragonRunner.RunAsync(profile.OneDragonConfigName, ensureGameStarted: false, cancellationToken);

                    await gameSessionController.CloseGameAsync(profile, cancellationToken);

                    record = CreateRecord(profile, startedAt, success: true, message: "Batch run completed successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Batch run failed for profile {ProfileName}", profile.Name);
                    record = CreateRecord(profile, startedAt, success: false, message: ex.Message);
                    records.Add(record);
                    await AppendLogAsync(logPath, record, cancellationToken);
                    break;
                }

                records.Add(record);
                await AppendLogAsync(logPath, record, cancellationToken);
            }
        }
        finally
        {
            TaskContext.Instance().ClearRuntimeLaunchContext();
        }

        return records;
    }

    private void ValidateProfile(MultiAccountProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            throw new InvalidOperationException($"Install path is missing for profile {profile.Name}.");
        }

        if (string.IsNullOrWhiteSpace(profile.OneDragonConfigName))
        {
            throw new InvalidOperationException($"OneDragon config is missing for profile {profile.Name}.");
        }

        if (!oneDragonConfigCatalog.Exists(profile.OneDragonConfigName))
        {
            throw new InvalidOperationException(
                $"OneDragon config does not exist: {profile.OneDragonConfigName}. Open the OneDragon page and save it first.");
        }

        if (!profile.Region.SupportsRememberedAccountUiSwitch())
        {
            throw new InvalidOperationException(
                $"Remembered-account UI switching is not supported for {profile.Region}.");
        }

        if (string.IsNullOrWhiteSpace(profile.RememberedAccountLabel))
        {
            throw new InvalidOperationException(
                $"Remembered account label is missing for profile {profile.Name}. Fill in the masked account text before running.");
        }
    }

    private static BatchRunRecord CreateRecord(MultiAccountProfile profile, DateTimeOffset startedAt, bool success, string message)
    {
        return new BatchRunRecord
        {
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            Region = profile.Region,
            StartedAt = startedAt,
            EndedAt = DateTimeOffset.UtcNow,
            Success = success,
            Message = message,
        };
    }

    private static async Task AppendLogAsync(string logPath, BatchRunRecord record, CancellationToken cancellationToken)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(record) + Environment.NewLine;
        await File.AppendAllTextAsync(logPath, json, cancellationToken);
    }
}
