using Cortex.Mediator;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Domain.Contracts;

namespace TicTacToe.Infrastructure.Data;

/// <summary>
/// The single commit point: saves, then dispatches collected domain events on the mediator
/// only after a successful save (cqrs-and-mediator §5).
/// </summary>
public class ApplicationUnitOfWork(
    ApplicationDbContext dbContext,
    IGameRepository gameRepository,
    IPlayerProfileRepository playerProfileRepository,
    IMediator mediator) : UnitOfWork(dbContext), IApplicationUnitOfWork
{
    public IGameRepository GameRepository => gameRepository;

    public IPlayerProfileRepository PlayerProfileRepository => playerProfileRepository;

    public new async Task SaveAsync()
    {
        var aggregatesWithEvents = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregatesWithEvents.SelectMany(a => a.DomainEvents).ToList();

        await base.SaveAsync();

        foreach (var domainEvent in events)
        {
            // Dynamic dispatch binds PublishAsync to the concrete wrapper type, so
            // INotificationHandler<DomainEventNotification<TEvent>> handlers receive it.
            await mediator.PublishAsync(DynamicNotification.Wrap(domainEvent));
        }

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }

    private static class DynamicNotification
    {
        // Separate dynamic wrapper so PublishAsync resolves the concrete notification type.
        public static dynamic Wrap(IDomainEvent domainEvent) => TicTacToe.Application.Common.Events.DomainEventNotificationFactory.Wrap(domainEvent);
    }
}
