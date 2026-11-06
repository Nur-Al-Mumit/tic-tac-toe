using TicTacToe.Domain.Contracts;
using TicTacToe.Domain.Entities;

namespace TicTacToe.Application.Contracts.Repositories;

public interface IPlayerProfileRepository : IRepository<PlayerProfile, Guid>
{
    Task<IReadOnlyList<PlayerProfile>> GetByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
