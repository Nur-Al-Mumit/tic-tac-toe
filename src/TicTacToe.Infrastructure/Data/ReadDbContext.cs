using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Domain.Entities;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Infrastructure.Data;

/// <summary>Thin no-tracking view over the same ApplicationDbContext for the query side.</summary>
public class ReadDbContext(ApplicationDbContext context) : IReadDbContext
{
    public IQueryable<Game> Games => context.Games.AsNoTracking();

    public IQueryable<PlayerProfile> PlayerProfiles => context.PlayerProfiles.AsNoTracking();
}
