using NUnit.Framework;
using Shouldly;
using TicTacToe.Domain.Exceptions;
using TicTacToe.Domain.Games;

namespace TicTacToe.Domain.UnitTests.Games;

public class GameTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Host = Guid.NewGuid();
    private static readonly Guid Opponent = Guid.NewGuid();

    private static Game NewStartedGame(out Guid host, out Guid opponent)
    {
        host = Guid.NewGuid();
        opponent = Guid.NewGuid();
        var game = Game.Create(host, Now);
        game.Join(opponent, Now);
        return game;
    }

    // ---- Create -----------------------------------------------------------

    [Test]
    public void Create_SetsHostAsX_Waiting_RaisesGameCreated()
    {
        var game = Game.Create(Host, Now);

        game.PlayerXId.ShouldBe(Host);
        game.PlayerOId.ShouldBeNull();
        game.Status.ShouldBe(GameStatus.Waiting);
        game.CurrentTurn.ShouldBe(Mark.X);
        game.Result.ShouldBeNull();
        game.DomainEvents.OfType<GameCreated>().Single().HostPlayerId.ShouldBe(Host);
    }

    [Test]
    public void Create_EmptyHost_Throws()
    {
        Should.Throw<ArgumentException>(() => Game.Create(Guid.Empty, Now));
    }

    // ---- Join ---------------------------------------------------------------

    [Test]
    public void Join_AddsPlayerO_StartsGame_RaisesGameStarted()
    {
        var game = Game.Create(Host, Now);

        game.Join(Opponent, Now.AddSeconds(5));

        game.PlayerOId.ShouldBe(Opponent);
        game.Status.ShouldBe(GameStatus.InProgress);
        game.StartedAtUtc.ShouldBe(Now.AddSeconds(5));
        game.DomainEvents.OfType<GameStarted>().Single().PlayerOId.ShouldBe(Opponent);
    }

    [Test]
    public void Join_OwnGame_Throws()
    {
        var game = Game.Create(Host, Now);

        Should.Throw<CannotJoinOwnGameException>(() => game.Join(Host, Now));
        game.Status.ShouldBe(GameStatus.Waiting);
    }

    [Test]
    public void Join_AlreadyStarted_ThrowsGameFull()
    {
        var game = NewStartedGame(out _, out _);

        Should.Throw<GameFullException>(() => game.Join(Guid.NewGuid(), Now));
    }

    [Test]
    public void Join_FinishedGame_ThrowsGameFull()
    {
        var game = FinishedByXWin();

        Should.Throw<GameFullException>(() => game.Join(Guid.NewGuid(), Now));
    }

    // ---- Moves: guard rules (FR-08) -----------------------------------------

    [Test]
    public void Move_BeforeJoin_ThrowsNotInProgress()
    {
        var game = Game.Create(Host, Now);

        Should.Throw<GameNotInProgressException>(() => game.MakeMove(Host, 0, Now));
    }

    [Test]
    public void Move_XAlwaysMovesFirst()
    {
        var game = NewStartedGame(out _, out var opponent);

        Should.Throw<NotYourTurnException>(() => game.MakeMove(opponent, 0, Now));
        game.Moves.ShouldBeEmpty();
    }

    [Test]
    public void Move_AlternatesTurns()
    {
        var game = NewStartedGame(out var host, out var opponent);

        game.MakeMove(host, 0, Now);
        game.MakeMove(opponent, 3, Now);
        game.MakeMove(host, 1, Now);

        game.Moves.Count.ShouldBe(3);
        game.CurrentTurn.ShouldBe(Mark.O);
        Should.Throw<NotYourTurnException>(() => game.MakeMove(host, 5, Now));
    }

    [TestCase(-1)]
    [TestCase(9)]
    [TestCase(42)]
    public void Move_OutOfRange_Throws(int position)
    {
        var game = NewStartedGame(out var host, out _);

        Should.Throw<InvalidPositionException>(() => game.MakeMove(host, position, Now));
    }

    [Test]
    public void Move_OccupiedCell_Throws()
    {
        var game = NewStartedGame(out var host, out var opponent);

        game.MakeMove(host, 4, Now);
        game.MakeMove(opponent, 0, Now);

        Should.Throw<CellOccupiedException>(() => game.MakeMove(host, 4, Now));
        game.Moves.Count.ShouldBe(2);
    }

    [Test]
    public void Move_RaisesMoveMadeEvent_EveryTime()
    {
        var game = NewStartedGame(out var host, out var opponent);

        game.MakeMove(host, 0, Now);
        game.MakeMove(opponent, 1, Now);

        var events = game.DomainEvents.OfType<MoveMade>().ToList();
        events.Count.ShouldBe(2);
        events[0].Mark.ShouldBe(Mark.X);
        events[0].MoveNumber.ShouldBe(1);
        events[1].Mark.ShouldBe(Mark.O);
        events[1].MoveNumber.ShouldBe(2);
    }

    // ---- Win detection: all 8 lines (FR-09) ----------------------------------

    [TestCase(new[] { 0, 1, 2 })] // top row
    [TestCase(new[] { 3, 4, 5 })] // middle row
    [TestCase(new[] { 6, 7, 8 })] // bottom row
    [TestCase(new[] { 0, 3, 6 })] // left column
    [TestCase(new[] { 1, 4, 7 })] // middle column
    [TestCase(new[] { 2, 5, 8 })] // right column
    [TestCase(new[] { 0, 4, 8 })] // main diagonal
    [TestCase(new[] { 2, 4, 6 })] // anti diagonal
    public void Win_XCompletesLine_FinishesWithWinningLine(int[] line)
    {
        var game = NewStartedGame(out var host, out _);

        foreach (var position in line)
        {
            game.MakeMove(host, position, Now);
            if (game.Status != GameStatus.InProgress)
            {
                break;
            }

            game.MakeMove(PlayerOOf(game), FirstFreeOutside(line, game), Now);
        }

        game.Status.ShouldBe(GameStatus.Finished);
        game.Result!.Outcome.ShouldBe(GameOutcome.XWins);
        game.Result.Reason.ShouldBe(FinishReason.Normal);
        game.Result.WinningLine.ShouldNotBeNull();
        game.Result.WinningLine!.OrderBy(p => p).ToArray().ShouldBe(line.OrderBy(p => p).ToArray());
        game.DomainEvents.OfType<GameFinished>().Count().ShouldBe(1);
    }

    [Test]
    public void Win_OWins_WhenOCompletesLine()
    {
        var game = NewStartedGame(out _, out var opponent);

        // X: 0,3,6 — O: 2,4 — X: 8 is blocked by O taking 6? Keep simple: O completes 2,5,8 while X scatters.
        game.MakeMove(game.PlayerXId, 0, Now);
        game.MakeMove(opponent, 2, Now);
        game.MakeMove(game.PlayerXId, 1, Now);
        game.MakeMove(opponent, 5, Now);
        game.MakeMove(game.PlayerXId, 7, Now); // blocks nothing that matters; X has 0,1,7
        game.MakeMove(opponent, 8, Now);       // O completes 2,5,8

        game.Status.ShouldBe(GameStatus.Finished);
        game.Result!.Outcome.ShouldBe(GameOutcome.OWins);
        game.Result.WinningLine.ShouldNotBeNull();
    }

    [Test]
    public void Draw_NineMovesNoWinner()
    {
        var game = NewStartedGame(out var host, out var opponent);

        // Verified draw: X {1,3,4,6,8}, O {0,2,5,7} — no line completes at any intermediate step.
        (int Mark, int Position)[] sequence =
        [
            (0, 4), (1, 0), (0, 1), (1, 2), (0, 3), (1, 5), (0, 6), (1, 7), (0, 8),
        ];
        foreach (var (mark, position) in sequence)
        {
            var player = mark == 0 ? host : opponent;
            game.MakeMove(player, position, Now);
        }

        game.Status.ShouldBe(GameStatus.Finished);
        game.Result!.Outcome.ShouldBe(GameOutcome.Draw);
        game.Result.Reason.ShouldBe(FinishReason.Normal);
        game.Result.WinningLine.ShouldBeNull();
        game.Moves.Count.ShouldBe(9);
    }

    // ---- Finished games are immutable (invariant 5) ---------------------------

    [Test]
    public void FinishedGame_IsImmutable()
    {
        var game = FinishedByXWin();

        Should.Throw<GameNotInProgressException>(() => game.MakeMove(game.PlayerXId, 8, Now));
        Should.Throw<GameNotInProgressException>(() => game.Forfeit(game.PlayerXId, Now));
        Should.Throw<GameFullException>(() => game.Join(Guid.NewGuid(), Now));
        Should.Throw<GameNotInProgressException>(() => game.Cancel(game.PlayerXId, Now));
        game.Result.ShouldNotBeNull();
    }

    // ---- Forfeit / Cancel ------------------------------------------------------

    [Test]
    public void Forfeit_XForfeits_OWinsByForfeit()
    {
        var game = NewStartedGame(out var host, out _);

        game.Forfeit(host, Now);

        game.Status.ShouldBe(GameStatus.Finished);
        game.Result!.Outcome.ShouldBe(GameOutcome.OWins);
        game.Result.Reason.ShouldBe(FinishReason.Forfeit);
        game.Result.WinningLine.ShouldBeNull();
        game.DomainEvents.OfType<GameFinished>().Single().WinnerPlayerId.ShouldBe(game.PlayerOId);
    }

    [Test]
    public void Forfeit_OForfeits_XWinsByForfeit()
    {
        var game = NewStartedGame(out _, out var opponent);

        game.Forfeit(opponent, Now);

        game.Result!.Outcome.ShouldBe(GameOutcome.XWins);
    }

    [Test]
    public void Cancel_HostCancelsWaitingGame()
    {
        var game = Game.Create(Host, Now);

        game.Cancel(Host, Now);

        game.Status.ShouldBe(GameStatus.Cancelled);
        game.FinishedAtUtc.ShouldNotBeNull();
        game.DomainEvents.OfType<GameCancelled>().Single().HostPlayerId.ShouldBe(Host);
    }

    [Test]
    public void Cancel_WhileInProgress_Throws()
    {
        var game = NewStartedGame(out var host, out _);

        Should.Throw<GameNotInProgressException>(() => game.Cancel(host, Now));
    }

    [Test]
    public void IsParticipant_MatchesBothPlayersOnly()
    {
        var game = NewStartedGame(out var host, out var opponent);

        game.IsParticipant(host).ShouldBeTrue();
        game.IsParticipant(opponent).ShouldBeTrue();
        game.IsParticipant(Guid.NewGuid()).ShouldBeFalse();
    }

    // ---- helpers ---------------------------------------------------------------

    private static Guid PlayerOOf(Game game) => game.PlayerOId!.Value;

    private static int FirstFreeOutside(int[] line, Game game)
    {
        for (var p = 0; p < 9; p++)
        {
            if (!line.Contains(p) && game.Moves.All(m => m.Position != p))
            {
                return p;
            }
        }

        throw new InvalidOperationException("board exhausted");
    }

    private static Game FinishedByXWin()
    {
        var game = NewStartedGame(out var host, out var opponent);
        game.MakeMove(host, 0, Now);
        game.MakeMove(opponent, 3, Now);
        game.MakeMove(host, 1, Now);
        game.MakeMove(opponent, 4, Now);
        game.MakeMove(host, 2, Now); // X wins row 0-1-2
        return game;
    }
}
