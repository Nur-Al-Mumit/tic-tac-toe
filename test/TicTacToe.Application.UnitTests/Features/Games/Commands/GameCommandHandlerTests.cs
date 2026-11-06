using Microsoft.Extensions.Options;
using NUnit.Framework;
using NSubstitute;
using Shouldly;
using TicTacToe.Application.Common;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games;
using TicTacToe.Application.Features.Games.Command;
using TicTacToe.Domain.Entities;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.UnitTests.Features.Games.Commands;

public class GameCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private sealed record Ctx(
        IApplicationUnitOfWork UnitOfWork,
        IGameRepository Games,
        IPlayerProfileRepository Profiles,
        ICurrentUser CurrentUser,
        IDateTimeProvider Time,
        GameDtoLoader Loader,
        IOptions<GameRulesOptions> Rules);

    private static Ctx New()
    {
        var games = Substitute.For<IGameRepository>();
        var profiles = Substitute.For<IPlayerProfileRepository>();
        var unitOfWork = Substitute.For<IApplicationUnitOfWork>();
        var currentUser = Substitute.For<ICurrentUser>();
        var time = Substitute.For<IDateTimeProvider>();

        unitOfWork.GameRepository.Returns(games);
        unitOfWork.PlayerProfileRepository.Returns(profiles);
        time.UtcNow.Returns(Now);
        profiles.GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IReadOnlyCollection<Guid>>()
                .Select(id => new PlayerProfile(id, id == Ids.Host ? "host_01" : "opp_02", Now))
                .ToList());

        var rules = Substitute.For<IOptions<GameRulesOptions>>();
        rules.Value.Returns(new GameRulesOptions());

        return new Ctx(unitOfWork, games, profiles, currentUser, time, new GameDtoLoader(Substitute.For<Contracts.Services.IReadDbContext>(), profiles), rules);
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
    public async Task GameCreate_WhenPlayerAlreadyActive_ReturnsGamesBusy()
    {
        var ctx = New();
        ctx.CurrentUser.UserId.Returns(Ids.Host);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Host, Arg.Any<CancellationToken>())
            .Returns(Game.Create(Ids.Host, Now));

        var result = await new GameCreateCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new GameCreateCommand(), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Busy");
        await ctx.Games.DidNotReceive().AddAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GameCreate_NewGame_IsWaitingWithHostAsX()
    {
        var ctx = New();
        ctx.CurrentUser.UserId.Returns(Ids.Host);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Host, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var result = await new GameCreateCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader)
            .Handle(new GameCreateCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("Waiting");
        result.Value.PlayerX.Username.ShouldBe("host_01");
        result.Value.PlayerO.ShouldBeNull();
        await ctx.Games.Received(1).AddAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).SaveAsync();
    }

    [Test]
    public async Task GameJoin_FullGame_ReturnsGamesFull()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Guid.NewGuid());

        var result = await new GameJoinCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameJoinCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Full");
    }

    [Test]
    public async Task GameJoin_OwnGame_ReturnsCannotJoinOwnGame()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Host);

        var result = await new GameJoinCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameJoinCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.CannotJoinOwnGame");
    }

    [Test]
    public async Task GameJoin_WaitingGame_StartsGame()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.Games.GetActiveGameForPlayerAsync(Ids.Opponent, Arg.Any<CancellationToken>()).Returns((Game?)null);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent);

        var result = await new GameJoinCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameJoinCommand(game.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("InProgress");
        result.Value.PlayerO!.Username.ShouldBe("opp_02");
        await ctx.UnitOfWork.Received(1).SaveAsync();
    }

    [Test]
    public async Task GameMove_NotYourTurn_ReturnsGamesNotYourTurn()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent); // O tries to move first

        var result = await new GameMoveCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameMoveCommand(game.Id, 0), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.NotYourTurn");
    }

    [Test]
    public async Task GameMove_NonParticipant_ReturnsGamesForbidden()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Guid.NewGuid());

        var result = await new GameMoveCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameMoveCommand(game.Id, 0), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Forbidden");
    }

    [Test]
    public async Task GameMove_ValidMove_UpdatesBoardAndTurn()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Host);

        var result = await new GameMoveCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time, ctx.Loader, ctx.Rules)
            .Handle(new GameMoveCommand(game.Id, 4), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Board[4].ShouldBe("X");
        result.Value.CurrentTurn.ShouldBe("O");
    }

    [Test]
    public async Task GameCancel_ByNonHost_ReturnsGamesNotHost()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.CurrentUser.UserId.Returns(Ids.Opponent);

        var result = await new GameCancelCommandHandler(ctx.UnitOfWork, ctx.CurrentUser, ctx.Time)
            .Handle(new GameCancelCommand(game.Id), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.NotHost");
    }

    [Test]
    public async Task GameTimeout_ExpiredClock_EndsGameByTimeout()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        game.Join(Ids.Opponent, Now, TimeSpan.FromSeconds(60)); // deadline: Now+60
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);
        ctx.Time.UtcNow.Returns(Now.AddSeconds(61)); // the clock has expired by handler time

        var result = await new GameTimeoutCommandHandler(ctx.UnitOfWork, ctx.Time)
            .Handle(new GameTimeoutCommand(game.Id, Ids.Host), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        game.Status.ShouldBe(GameStatus.Finished);
        game.Result!.Reason.ShouldBe(FinishReason.Timeout);
        game.Result.Outcome.ShouldBe(GameOutcome.OWins);
    }

    [Test]
    public async Task GameTimeout_ClockAlreadyReset_IsNoOp()
    {
        var ctx = New();
        var game = Game.Create(Ids.Host, Now);
        game.Join(Ids.Opponent, Now, TimeSpan.FromSeconds(6000)); // plenty of time left
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var result = await new GameTimeoutCommandHandler(ctx.UnitOfWork, ctx.Time)
            .Handle(new GameTimeoutCommand(game.Id, Ids.Host), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        game.Status.ShouldBe(GameStatus.InProgress);
        await ctx.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Test]
    public async Task GameLeave_ByNonParticipant_ReturnsGamesForbidden()
    {
        var ctx = New();
        var game = StartedGame();
        ctx.Games.GetGameByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var result = await new GameLeaveCommandHandler(ctx.UnitOfWork, ctx.Time)
            .Handle(new GameLeaveCommand(game.Id, Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.ShouldBe("Games.Forbidden");
    }
}
