namespace BetterGenshinImpact.Modules.MultiAccount;

public interface IMultiAccountProfileStore
{
    Task<IReadOnlyList<MultiAccountProfile>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default);

    Task SaveAllAsync(IEnumerable<MultiAccountProfile> profiles, CancellationToken cancellationToken = default);

    Task DeleteAsync(string profileId, CancellationToken cancellationToken = default);
}
