using TicTacToe.Domain.Contracts;
using TicTacToe.Domain.Exceptions;

namespace TicTacToe.Domain.Entities.GameAggregate;

/// <summary>
/// The aggregate root (PRD §7.2): every game rule lives here and nowhere else.
/// Invariants: max two distinct players; moves only while InProgress; strict alternation from X;
/// a cell is marked once; a finished game is immutable; the result is set exactly once.
/// </summary>
public class Game : IAggregateRoot<Guid>, IHasDomainEvents
{
    private readonly List<Move> _moves = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; set; }

    private Game()
    {
    }

    private Game(Guid hostPlayerId, DateTime utcNow)
    {
        Id = Guid.NewGuid();
        PlayerXId = hostPlayerId;
        Status = GameStatus.Waiting;
        CurrentTurn = Mark.X;
        CreatedAtUtc = utcNow;
    }

    public Guid PlayerXId { get; private set; }

    public Guid? PlayerOId { get; private set; }

    public GameStatus Status { get; private set; }

    public Mark CurrentTurn { get; private set; }

    public GameResult? Result { get; private set; }

    public IReadOnlyList<Move> Moves => _moves;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    /// <summary>PostgreSQL xmin, used as the optimistic concurrency token (ADR-0006).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Host creates a game and is Player X; X always moves first (FR-05, FR-08).</summary>
    public static Game Create(Guid hostPlayerId, DateTime utcNow)
    {
        if (hostPlayerId == Guid.Empty)
        {
            throw new ArgumentException("Host player id is required.", nameof(hostPlayerId));
        }

        var game = new Game(hostPlayerId, utcNow);
        game._domainEvents.Add(new GameCreated(game.Id, hostPlayerId, utcNow));
        return game;
    }

    /// <summary>Joiner becomes Player O; the game starts. Race safety comes from xmin at save time (FR-06).</summary>
    public void Join(Guid playerId, DateTime utcNow)
    {
        if (Status == GameStatus.Finished || Status == GameStatus.Cancelled)
        {
            throw new GameFullException(); // finished/cancelled games are not joinable — same user-facing outcome as full
        }

        if (Status != GameStatus.Waiting)
        {
            throw new GameFullException();
        }

        if (playerId == PlayerXId)
        {
            throw new CannotJoinOwnGameException();
        }

        PlayerOId = playerId;
        Status = GameStatus.InProgress;
        StartedAtUtc = utcNow;
        _domainEvents.Add(new GameStarted(Id, PlayerXId, playerId, utcNow));
    }

    /// <summary>Server-authoritative move: turn, bounds, occupancy and status are enforced here (FR-08, FR-09).</summary>
    public void MakeMove(Guid playerId, int position, DateTime utcNow)
    {
        if (Status != GameStatus.InProgress)
        {
            throw new GameNotInProgressException();
        }

        if (position is < 0 or > 8)
        {
            throw new InvalidPositionException(position);
        }

        var mark = MarkOf(playerId);
        if (mark != CurrentTurn)
        {
            throw new NotYourTurnException();
        }

        if (_moves.Any(m => m.Position == position))
        {
            throw new CellOccupiedException(position);
        }

        _moves.Add(new Move(Id, _moves.Count + 1, position, mark, playerId, utcNow));
        _domainEvents.Add(new MoveMade(Id, playerId, mark, position, _moves.Count, utcNow));

        var winningLine = Board.WinningLine(_moves);
        if (winningLine is not null)
        {
            Finish(mark == Mark.X ? GameOutcome.XWins : GameOutcome.OWins, FinishReason.Normal, winningLine, utcNow);
        }
        else if (_moves.Count == 9)
        {
            Finish(GameOutcome.Draw, FinishReason.Normal, null, utcNow);
        }
        else
        {
            CurrentTurn = mark == Mark.X ? Mark.O : Mark.X;
        }
    }

    /// <summary>The leaver loses; the opponent wins with reason Forfeit (US-10's manual V1 form).</summary>
    public void Forfeit(Guid playerId, DateTime utcNow)
    {
        if (Status != GameStatus.InProgress)
        {
            throw new GameNotInProgressException();
        }

        var loser = MarkOf(playerId);
        Finish(loser == Mark.X ? GameOutcome.OWins : GameOutcome.XWins, FinishReason.Forfeit, null, utcNow);
    }

    /// <summary>Only the host, only while Waiting (the host check runs in the handler as authorization).</summary>
    public void Cancel(Guid hostPlayerId, DateTime utcNow)
    {
        if (hostPlayerId != PlayerXId)
        {
            throw new GameNotInProgressException(); // non-hosts never reach here; handler rejects with Games.NotHost
        }

        if (Status != GameStatus.Waiting)
        {
            throw new GameNotInProgressException();
        }

        Status = GameStatus.Cancelled;
        FinishedAtUtc = utcNow;
        _domainEvents.Add(new GameCancelled(Id, PlayerXId, utcNow));
    }

    public bool IsParticipant(Guid playerId) => playerId == PlayerXId || playerId == PlayerOId;

    private Mark MarkOf(Guid playerId) => playerId == PlayerXId ? Mark.X : Mark.O;

    private void Finish(GameOutcome outcome, FinishReason reason, int[]? winningLine, DateTime utcNow)
    {
        Status = GameStatus.Finished;
        Result = new GameResult(outcome, reason, winningLine);
        FinishedAtUtc = utcNow;

        var winner = outcome switch
        {
            GameOutcome.XWins => PlayerXId,
            GameOutcome.OWins => PlayerOId,
            _ => null,
        };
        _domainEvents.Add(new GameFinished(Id, outcome, reason, winningLine, winner, utcNow));
    }
}
