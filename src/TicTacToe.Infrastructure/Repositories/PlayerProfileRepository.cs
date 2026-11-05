using Microsoft.EntityFrameworkCore;
using TicTacToe.Domain.Players;
using TicTacToe.Infrastructure.Persistence;

namespace TicTacToe.Infrastructure.Repositories;

public class PlayerProfileRepository(AppDbContext context) : IPlayerProfileRepository
{
    public async Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default)
        => await context.PlayerProfiles.AddAsync(profile, cancellationToken);

    public async Task<IReadOnlyList<PlayerProfile>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        => await context.PlayerProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
}
