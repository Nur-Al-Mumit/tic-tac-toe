using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Domain.Entities;
using TicTacToe.Infrastructure.Data;

namespace TicTacToe.Infrastructure.Data.Repositories;

public class PlayerProfileRepository(ApplicationDbContext context) : Repository<PlayerProfile, Guid>(context), IPlayerProfileRepository
{
    public async Task<IReadOnlyList<PlayerProfile>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => await context.PlayerProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
}
