using BetterGenshinImpact.Core.Config;

namespace BetterGenshinImpact.Modules.MultiAccount.Runner;

public class OneDragonConfigCatalog(string? configDirectory = null)
{
    public string ConfigDirectory { get; } = configDirectory ?? Global.Absolute(@"User\OneDragon");

    public IReadOnlyList<string> GetAvailableConfigNames()
    {
        Directory.CreateDirectory(ConfigDirectory);
        return Directory.GetFiles(ConfigDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public string GetConfigPath(string configName)
    {
        return Path.Combine(ConfigDirectory, $"{configName}.json");
    }

    public bool Exists(string? configName)
    {
        return !string.IsNullOrWhiteSpace(configName)
               && File.Exists(GetConfigPath(configName));
    }
}
