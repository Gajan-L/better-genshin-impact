namespace BetterGenshinImpact.Modules.MultiAccount;

public static class MultiAccountCommandLineProfileSelector
{
    public static IReadOnlyList<MultiAccountProfile> SelectProfiles(
        IReadOnlyList<MultiAccountProfile> profiles,
        IEnumerable<string>? requestedProfileNames = null)
    {
        var requestedNames = requestedProfileNames?
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray() ?? [];

        if (requestedNames.Length == 0)
        {
            return profiles
                .Where(profile => profile.IsEnabled)
                .OrderBy(profile => profile.Order)
                .ToList();
        }

        var selectedProfiles = new List<MultiAccountProfile>();
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requestedName in requestedNames)
        {
            var profile = ResolveProfile(profiles, requestedName);
            if (selectedIds.Add(profile.Id))
            {
                selectedProfiles.Add(profile);
            }
        }

        return selectedProfiles;
    }

    private static MultiAccountProfile ResolveProfile(IReadOnlyList<MultiAccountProfile> profiles, string requestedName)
    {
        var idMatch = profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, requestedName, StringComparison.OrdinalIgnoreCase));
        if (idMatch != null)
        {
            return idMatch;
        }

        var nameMatches = profiles
            .Where(profile => string.Equals(profile.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(profile => profile.Order)
            .ToList();

        return nameMatches.Count switch
        {
            1 => nameMatches[0],
            > 1 => throw new InvalidOperationException(
                $"Multi-account profile name is ambiguous: {requestedName}. Use the profile id instead."),
            _ => throw new InvalidOperationException(
                $"Multi-account profile does not exist: {requestedName}. Use --listMultiAccountProfiles to inspect available profiles."),
        };
    }
}
