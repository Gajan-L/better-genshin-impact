using System.Text.Json;
using System.Text.Json.Serialization;
using BetterGenshinImpact.Service;

namespace BetterGenshinImpact.Modules.MultiAccount;

public class MultiAccountProfileStore(string? profilesDirectory = null) : IMultiAccountProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _profilesDirectory = profilesDirectory ?? MultiAccountPaths.ProfilesDirectory;

    public async Task<IReadOnlyList<MultiAccountProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_profilesDirectory);

        var profiles = new List<MultiAccountProfile>();
        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var profile = DeserializeProfile(json);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        return profiles
            .OrderBy(profile => profile.Order)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SaveAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_profilesDirectory);
        var path = GetProfilePath(profile.Id);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
    }

    public async Task SaveAllAsync(IEnumerable<MultiAccountProfile> profiles, CancellationToken cancellationToken = default)
    {
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SaveAsync(profile, cancellationToken);
        }
    }

    public Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var path = GetProfilePath(profileId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetProfilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(ConfigService.JsonOptions);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static MultiAccountProfile? DeserializeProfile(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MultiAccountProfile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            if (!json.Contains("\"CNBilibili\"", StringComparison.Ordinal))
            {
                throw;
            }

            var migratedJson = json.Replace("\"CNBilibili\"", "\"CNOfficial\"", StringComparison.Ordinal);
            return JsonSerializer.Deserialize<MultiAccountProfile>(migratedJson, JsonOptions);
        }
    }
}
