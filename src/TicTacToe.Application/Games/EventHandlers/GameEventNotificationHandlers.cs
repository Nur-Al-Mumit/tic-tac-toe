using Cortex.Mediator.Notifications;
using TicTacToe.Application.Common.Events;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Domain.Games;

namespace TicTacToe.Application.Games.EventHandlers;

/// <summary>Post-commit fan-out: each domain event becomes the matching IGameNotifier push (cqrs-and-mediator §5).</summary>

public sealed class GameStartedNotificationHandler(IGameNotifier notifier, GameDtoLoader dtoLoader)
    : INotificationHandler<DomainEventNotification<GameStarted>>
{
    public async Task Handle(DomainEventNotification<GameStarted> notification, CancellationToken cancellationToken)
    {
        var dto = await dtoLoader.LoadByIdAsync(notification.DomainEvent.GameId, cancellationToken);
        if (dto is not null)
        {
            await notifier.GameStartedAsync(dto, cancellationToken);
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
    }
}

public sealed class GameCancelledNotificationHandler(IGameNotifier notifier)
    : INotificationHandler<DomainEventNotification<GameCancelled>>
{
    public Task Handle(DomainEventNotification<GameCancelled> notification, CancellationToken cancellationToken)
        => notifier.GameCancelledAsync(notification.DomainEvent.GameId, cancellationToken);
}
