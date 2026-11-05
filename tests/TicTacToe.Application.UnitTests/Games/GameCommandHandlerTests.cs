using NUnit.Framework;
using NSubstitute;
using Shouldly;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games;
using TicTacToe.Application.Games.Commands;
using TicTacToe.Domain.Games;
using TicTacToe.Domain.Players;

namespace TicTacToe.Application.UnitTests.Games;

public class GameCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private sealed record Ctx(
        IGameRepository Games,
        IPlayerProfileRepository Profiles,
        IUnitOfWork UnitOfWork,
        IReadDbContext Read,
        ICurrentUser CurrentUser,
        IDateTimeProvider Time,
        GameDtoLoader Loader);

    private static Ctx New()
    {
        var games = Substitute.For<IGameRepository>();
        var profiles = Substitute.For<IPlayerProfileRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var read = Substitute.For<IReadDbContext>();
        var currentUser = Substitute.For<ICurrentUser>();
        var time = Substitute.For<IDateTimeProvider>();

        time.UtcNow.Returns(Now);
        profiles.GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyCollection<Guid>>()
                .Select(id => id == Ids.Host ? new PlayerProfile(id, "host_01", Now) : new PlayerProfile(id, "opp_02", Now))
                .ToList());

        return new Ctx(games, profiles, unitOfWork, read, currentUser, time, new GameDtoLoader(read, profiles));
    }

    private static Game StartedGame()
    {
        var game = Game.Create(Ids.Host, Now);
        game.Join(Ids.Opponent, Now);
        return game;
    }

    private static class Ids
    {
        public static readonly Guid Host = Guid.NewGuid();
        public static readonly Guid Opponent = Guid.NewGuid();
    }

    [Test]
    public async Task Create_WhenPlayerAlreadyActive_ReturnsGamesBusy()
    {
        var ctx = New();
        ctx.CurrentUser.UserId.Returns(Ids.Host);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Host, Arg.Any<CancellationToken>())
            .Returns(Game.Create(Ids.Host, Now));

        var result = await new CreateGameCommandHandler(ctx.Games, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new CreateGameCommand(), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Busy");
        await ctx.Games.DidNotReceive().AddAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Create_NewGame_IsWaitingWithHostAsX()
    {
        var ctx = New();
        ctx.CurrentUser.UserId.Returns(Ids.Host);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Host, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var result = await new CreateGameCommandHandler(ctx.Games, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new CreateGameCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("Waiting");
        result.Value.PlayerX.Username.ShouldBe("host_01");
        result.Value.PlayerO.ShouldBeNull();
        await ctx.Games.Received(1).AddAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Join_FullGame_ReturnsGamesFull()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Guid.NewGuid());

        var result = await new JoinGameCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Loader, ctx.Time)
            .Handle(new JoinGameCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Full");
    }

    [Test]
    public async Task Join_OwnGame_ReturnsCannotJoinOwnGame()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Host);

        var result = await new JoinGameCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Loader, ctx.Time)
            .Handle(new JoinGameCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.CannotJoinOwnGame");
    }

    [Test]
    public async Task Join_WaitingGame_StartsGame()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Opponent, Arg.Any<CancellationToken>()).Returns((Game?)null);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent);

        var result = await new JoinGameCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Loader, ctx.Time)
            .Handle(new JoinGameCommand(game.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("InProgress");
        result.Value.PlayerO!.Username.ShouldBe("opp_02");
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Move_NotYourTurn_ReturnsGamesNotYourTurn()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent); // O tries to move first

        var result = await new MakeGameMoveCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new MakeGameMoveCommand(game.Id, 0), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.NotYourTurn");
    }

    [Test]
    public async Task Move_NonParticipant_ReturnsGamesForbidden()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Guid.NewGuid());

        var result = await new MakeGameMoveCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new MakeGameMoveCommand(game.Id, 0), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Forbidden");
    }

    [Test]
    public async Task Move_ValidMove_UpdatesBoardAndTurn()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Host);

        var result = await new MakeGameMoveCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new MakeGameMoveCommand(game.Id, 4), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Board[4].ShouldBe("X");
        result.Value.CurrentTurn.ShouldBe("O");
    }

    [Test]
    public async Task Cancel_ByNonHost_ReturnsGamesNotHost()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent);

        var result = await new CancelGameCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.CurrentUser, ctx.Time)
            .Handle(new CancelGameCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.NotHost");
    }

    [Test]
    public async Task Forfeit_ByNonParticipant_ReturnsGamesForbidden()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var result = await new ForfeitGameCommandHandler(ctx.Games, ctx.UnitOfWork, ctx.Time)
            .Handle(new ForfeitGameCommand(game.Id, Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Forbidden");
    }
}
