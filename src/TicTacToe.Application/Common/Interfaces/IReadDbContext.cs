using TicTacToe.Domain.Games;
using TicTacToe.Domain.Players;

namespace TicTacToe.Application.Common.Interfaces;

/// <summary>Read-side projection surface: already no-tracking, never materializes aggregates for writes (cqrs-and-mediator §1).</summary>
public interface IReadDbContext
{
    IQueryable<Game> Games { get; }

    IQueryable<PlayerProfile> PlayerProfiles { get; }
}
