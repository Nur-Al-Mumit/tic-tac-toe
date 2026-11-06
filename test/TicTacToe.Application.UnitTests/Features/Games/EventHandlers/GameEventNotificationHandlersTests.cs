using NSubstitute;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Application.Features.Games;
using TicTacToe.Application.Features.Games.EventHandlers;
using TicTacToe.Application.Common.Events;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.UnitTests.Features.Games.EventHandlers;

public class GameEventNotificationHandlersTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid GameId = Guid.NewGuid();

    private static GameDto Dto() => new(GameId, "Waiting",
        new PlayerRef(Guid.NewGuid(), "host_01"), null,
        new string?[9], "X", null, Now);

    [Test]
    public async Task GameCreated_PushesToLobbyList()
    {
        var notifier = Substitute.For<IGameNotifier>();
        var loader = Substitute.For<GameDtoLoader>(null!, null!);
        loader.LoadByIdAsync(GameId, Arg.Any<CancellationToken>()).Returns(Dto());

        await new GameCreatedNotificationHandler(notifier, loader)
            .Handle(new DomainEventNotification<GameCreated>(new GameCreated(GameId, Guid.NewGuid(), Now)), CancellationToken.None);

        await notifier.Received(1).LobbyGameAddedAsync(Arg.Any<GameDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GameStarted_NotifiesGameRoom_AndRemovesFromLobby()
    {
        var notifier = Substitute.For<IGameNotifier>();
        var loader = Substitute.For<GameDtoLoader>(null!, null!);
        loader.LoadByIdAsync(GameId, Arg.Any<CancellationToken>()).Returns(Dto());

        await new GameStartedNotificationHandler(notifier, loader)
            .Handle(new DomainEventNotification<GameStarted>(new GameStarted(GameId, Guid.NewGuid(), Guid.NewGuid(), Now)), CancellationToken.None);

        await notifier.Received(1).GameStartedAsync(Arg.Any<GameDto>(), Arg.Any<CancellationToken>());
        await notifier.Received(1).LobbyGameRemovedAsync(GameId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GameCancelled_NotifiesRoom_AndRemovesFromLobby()
    {
        var notifier = Substitute.For<IGameNotifier>();

        await new GameCancelledNotificationHandler(notifier)
            .Handle(new DomainEventNotification<GameCancelled>(new GameCancelled(GameId, Guid.NewGuid(), Now)), CancellationToken.None);

        await notifier.Received(1).GameCancelledAsync(GameId, Arg.Any<CancellationToken>());
        await notifier.Received(1).LobbyGameRemovedAsync(GameId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GameFinished_NotifiesRoom_AndRemovesFromLobby()
    {
        var notifier = Substitute.For<IGameNotifier>();
        var loader = Substitute.For<GameDtoLoader>(null!, null!);
        loader.LoadByIdAsync(GameId, Arg.Any<CancellationToken>()).Returns(Dto());

        await new GameFinishedNotificationHandler(notifier, loader)
            .Handle(new DomainEventNotification<GameFinished>(new GameFinished(GameId, GameOutcome.XWins, FinishReason.Normal, [0, 1, 2], Guid.NewGuid(), Now)), CancellationToken.None);

        await notifier.Received(1).GameFinishedAsync(Arg.Any<GameDto>(), Arg.Any<CancellationToken>());
        await notifier.Received(1).LobbyGameRemovedAsync(GameId, Arg.Any<CancellationToken>());
    }
}
