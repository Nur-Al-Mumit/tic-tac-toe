using TicTacToe.Domain.Common;

namespace TicTacToe.Domain.Players;

/// <summary>The player-facing profile row (1:1 with the Identity user); future home of stats and rating.</summary>
public sealed class PlayerProfile : Entity<Guid>, IAggregateRoot
{
    private PlayerProfile()
    {
    }

    public PlayerProfile(Guid userId, string username, DateTime createdAtUtc)
    {
        Id = userId;
        Username = username;
        CreatedAtUtc = createdAtUtc;
    }

    public string Username { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
}
