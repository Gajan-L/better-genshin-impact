namespace BetterGenshinImpact.Modules.MultiAccount;

public class GameExecutablePathResolver(IEnumerable<string>? searchRoots = null)
{
    private static readonly string[] CommonRootDirectories =
    [
        "Games",
        "games",
        "Program Files",
        "Program Files (x86)",
    ];

    private static readonly string[] CnRelativeExecutablePaths =
    [
        Path.Combine("miHoYo Launcher", "games", "Genshin Impact Game", "YuanShen.exe"),
    ];

    private static readonly string[] GlobalRelativeExecutablePaths =
    [
        Path.Combine("HoYoPlay", "games", "Genshin Impact game", "GenshinImpact.exe"),
    ];

    private readonly IReadOnlyList<string> _searchRoots = searchRoots?.ToList() ?? BuildDefaultSearchRoots();

    public string? Find(GameRegion region, params string?[] preferredPaths)
    {
        foreach (var path in preferredPaths.Where(path => IsUsableInstallPath(region, path)))
        {
            return path;
        }

        foreach (var candidate in EnumerateCandidatePaths(region))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public bool IsUsableInstallPath(GameRegion region, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return File.Exists(path)
               && string.Equals(
                   Path.GetFileName(path),
                   region.GetDefaultExecutableName(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> EnumerateCandidatePaths(GameRegion region)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _searchRoots)
        {
            foreach (var relativePath in GetRelativeExecutablePaths(region))
            {
                var candidate = Path.Combine(root, relativePath);
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IReadOnlyList<string> BuildDefaultSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drive in DriveInfo.GetDrives().Where(static drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            var driveRoot = drive.RootDirectory.FullName;
            roots.Add(driveRoot);

            foreach (var commonRootDirectory in CommonRootDirectories)
            {
                AddIfExists(roots, Path.Combine(driveRoot, commonRootDirectory));
            }
        }

        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfExists(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        return roots.ToList();
    }

    private static void AddIfExists(HashSet<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            roots.Add(path);
        }
    }

    private static IReadOnlyList<string> GetRelativeExecutablePaths(GameRegion region)
    {
        return region switch
        {
            GameRegion.CNOfficial => CnRelativeExecutablePaths,
            GameRegion.Global => GlobalRelativeExecutablePaths,
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
        };
    }
}
