using Cortex.Mediator.Notifications;
using TicTacToe.Application.Common.Events;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.EventHandlers;

/// <summary>Post-commit fan-out: each domain event becomes the matching IGameNotifier push (cqrs-and-mediator §5).</summary>

public sealed class GameCreatedNotificationHandler(IGameNotifier notifier, GameDtoLoader dtoLoader)
    : INotificationHandler<DomainEventNotification<GameCreated>>
{
    public async Task Handle(DomainEventNotification<GameCreated> notification, CancellationToken cancellationToken)
    {
        var dto = await dtoLoader.LoadByIdAsync(notification.DomainEvent.GameId, cancellationToken);
        if (dto is not null)
        {
            await notifier.LobbyGameAddedAsync(dto, cancellationToken);
        }
    }
}

public sealed class GameStartedNotificationHandler(IGameNotifier notifier, GameDtoLoader dtoLoader)
    : INotificationHandler<DomainEventNotification<GameStarted>>
{
    public async Task Handle(DomainEventNotification<GameStarted> notification, CancellationToken cancellationToken)
    {
        var dto = await dtoLoader.LoadByIdAsync(notification.DomainEvent.GameId, cancellationToken);
        if (dto is not null)
        {
            await notifier.GameStartedAsync(dto, cancellationToken);
            // The game moves from the Open tab to In Progress — not removed from the lobby view.
            await notifier.LobbyGameUpdatedAsync(dto, cancellationToken);
        }
    }
}

public sealed class MoveMadeNotificationHandler(IGameNotifier notifier, GameDtoLoader dtoLoader)
    : INotificationHandler<DomainEventNotification<MoveMade>>
{
    public async Task Handle(DomainEventNotification<MoveMade> notification, CancellationToken cancellationToken)
    {
        var dto = await dtoLoader.LoadByIdAsync(notification.DomainEvent.GameId, cancellationToken);
        if (dto is not null)
        {
            await notifier.MoveMadeAsync(dto, cancellationToken);
        }
    }
}

public sealed class GameFinishedNotificationHandler(IGameNotifier notifier, GameDtoLoader dtoLoader)
    : INotificationHandler<DomainEventNotification<GameFinished>>
{
    public async Task Handle(DomainEventNotification<GameFinished> notification, CancellationToken cancellationToken)
    {
        var dto = await dtoLoader.LoadByIdAsync(notification.DomainEvent.GameId, cancellationToken);
        if (dto is not null)
        {
            await notifier.GameFinishedAsync(dto, cancellationToken);
        }

        await notifier.LobbyGameRemovedAsync(notification.DomainEvent.GameId, cancellationToken);
    }
}

public sealed class GameCancelledNotificationHandler(IGameNotifier notifier)
    : INotificationHandler<DomainEventNotification<GameCancelled>>
{
    public async Task Handle(DomainEventNotification<GameCancelled> notification, CancellationToken cancellationToken)
    {
        await notifier.GameCancelledAsync(notification.DomainEvent.GameId, cancellationToken);
        await notifier.LobbyGameRemovedAsync(notification.DomainEvent.GameId, cancellationToken);
    }
}
