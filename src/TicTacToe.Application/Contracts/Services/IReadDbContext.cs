using TicTacToe.Domain.Entities;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Contracts.Services;

/// <summary>Read-side projection surface: already no-tracking, never materializes aggregates for writes.</summary>
public interface IReadDbContext
{
    IQueryable<Game> Games { get; }

    IQueryable<PlayerProfile> PlayerProfiles { get; }
}
