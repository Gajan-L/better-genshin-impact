using BetterGenshinImpact.Helpers.Win32;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.Modules.MultiAccount;

public class MultiAccountCommandLineService(
    IMultiAccountProfileStore profileStore,
    AccountBatchOrchestrator batchOrchestrator,
    ILogger<MultiAccountCommandLineService> logger)
{
    public async Task<int> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profiles = await profileStore.LoadAsync(cancellationToken);
            if (profiles.Count == 0)
            {
                ConsoleHelper.WriteLine("No multi-account profiles found.");
                return 0;
            }

            ConsoleHelper.WriteLine("Multi-account profiles:");
            foreach (var profile in profiles.OrderBy(profile => profile.Order))
            {
                var enabledText = profile.IsEnabled ? "enabled" : "disabled";
                var rememberedAccount = string.IsNullOrWhiteSpace(profile.RememberedAccountLabel)
                    ? "-"
                    : profile.RememberedAccountLabel;
                var oneDragonConfig = string.IsNullOrWhiteSpace(profile.OneDragonConfigName)
                    ? "-"
                    : profile.OneDragonConfigName;

                ConsoleHelper.WriteLine(
                    $"{profile.Order + 1}. {profile.Name} | id={profile.Id} | {enabledText} | region={profile.Region} | remembered={rememberedAccount} | one-dragon={oneDragonConfig}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list multi-account profiles from the command line.");
            ConsoleHelper.WriteError(ex.Message);
            return 1;
        }
    }

    public async Task RunBatchAsync(IEnumerable<string>? requestedProfileNames = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var profiles = await profileStore.LoadAsync(cancellationToken);
            var selectedProfiles = MultiAccountCommandLineProfileSelector.SelectProfiles(
                profiles,
                requestedProfileNames);

            if (selectedProfiles.Count == 0)
            {
                ConsoleHelper.WriteError(
                    "No enabled multi-account profiles found. Enable at least one profile or pass explicit profile names.");
                return;
            }

            ConsoleHelper.WriteLine(
                $"Starting multi-account batch with {selectedProfiles.Count} profile(s): {string.Join(", ", selectedProfiles.Select(profile => profile.Name))}");

            var records = await batchOrchestrator.RunBatchAsync(selectedProfiles, cancellationToken);
            var failedRecord = records.FirstOrDefault(record => !record.Success);
            if (failedRecord != null)
            {
                ConsoleHelper.WriteError(
                    $"Multi-account batch stopped on '{failedRecord.ProfileName}': {failedRecord.Message}");
                return;
            }

            ConsoleHelper.WriteLine($"Multi-account batch completed: {records.Count} profile(s).");
        }
        catch (OperationCanceledException)
        {
            ConsoleHelper.WriteError("Multi-account batch was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run the multi-account batch from the command line.");
            ConsoleHelper.WriteError(ex.Message);
        }
    }
}
