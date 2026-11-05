using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Domain.Games;
using TicTacToe.Domain.Players;

namespace TicTacToe.Infrastructure.Persistence;

/// <summary>Thin no-tracking view over the same AppDbContext for the query side.</summary>
public class ReadDbContext(AppDbContext context) : IReadDbContext
{
    public IQueryable<Game> Games => context.Games.AsNoTracking();

    public IQueryable<PlayerProfile> PlayerProfiles => context.PlayerProfiles.AsNoTracking();
}
