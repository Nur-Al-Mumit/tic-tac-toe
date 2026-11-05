using Cortex.Mediator;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Events;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Domain.Common;

namespace TicTacToe.Infrastructure.Persistence;

/// <summary>Single commit point; domain events dispatch on the mediator only after a successful save.</summary>
public class UnitOfWork(AppDbContext context, IMediator mediator) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregatesWithEvents.SelectMany(a => a.DomainEvents).ToList();

        await context.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in events)
        {
            // Dynamic dispatch binds PublishAsync to the concrete wrapper type, so
            // INotificationHandler<DomainEventNotification<TEvent>> handlers receive it.
            await mediator.PublishAsync(DynamicNotification.Wrap(domainEvent), cancellationToken);
        }

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }

    private static class DynamicNotification
    {
        // Separate overload so the async state machine is generated per call site.
        public static dynamic Wrap(IDomainEvent domainEvent) => DomainEventNotificationFactory.Wrap(domainEvent);
    }
}
