using TicTacToe.Domain.Common;

namespace TicTacToe.Domain.Games;

/// <summary>Child entity of the Game aggregate; the board is derived from moves (no stored board).</summary>
public class Move : Entity<Guid>
{
    private Move()
    {
    }

    internal Move(Guid gameId, int moveNumber, int position, Mark mark, Guid playerId, DateTime playedAtUtc)
    {
        Id = Guid.NewGuid();
        GameId = gameId;
        MoveNumber = moveNumber;
        Position = position;
        Mark = mark;
        PlayerId = playerId;
        PlayedAtUtc = playedAtUtc;
    }

    public Guid GameId { get; private set; }

    public int MoveNumber { get; private set; }

    public int Position { get; private set; }

    public Mark Mark { get; private set; }

    public Guid PlayerId { get; private set; }

    public DateTime PlayedAtUtc { get; private set; }
}
