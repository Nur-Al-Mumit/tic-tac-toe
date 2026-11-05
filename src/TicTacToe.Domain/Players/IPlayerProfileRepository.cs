namespace TicTacToe.Domain.Players;

/// <summary>Repository contract in the Domain (speaks aggregates); implementation lives in Infrastructure.</summary>
public interface IPlayerProfileRepository
{
    Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerProfile>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
