namespace BetterGenshinImpact.Modules.MultiAccount;

public class BatchRunRecord
{
    public string ProfileId { get; init; } = string.Empty;

    public string ProfileName { get; init; } = string.Empty;

    public GameRegion Region { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset EndedAt { get; init; }

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
