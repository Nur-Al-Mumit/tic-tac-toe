using TicTacToe.Domain.Contracts;

namespace TicTacToe.Domain.Entities;

/// <summary>The player-facing profile row (1:1 with the Identity user); future home of stats and rating.</summary>
public sealed class PlayerProfile : IAggregateRoot<Guid>
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

    public Guid Id { get; set; }

    public string Username { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
}
