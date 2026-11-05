using Cortex.Mediator.Notifications;
using TicTacToe.Domain.Common;

namespace TicTacToe.Application.Common.Events;

/// <summary>
/// Wraps a pure Domain event as a Cortex notification, so the Domain references no packages.
/// Handlers subscribe to DomainEventNotification&lt;TConcreteEvent&gt;.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification where TEvent : IDomainEvent;

public static class DomainEventNotificationFactory
{
    public static INotification Wrap(IDomainEvent domainEvent)
    {
        var type = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)(Activator.CreateInstance(type, domainEvent)
            ?? throw new InvalidOperationException($"Could not wrap domain event {domainEvent.GetType().Name}."));
    }
}
