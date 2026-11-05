namespace TicTacToe.Domain.Common;

/// <summary>A domain event raised inside an aggregate; dispatched only after a successful commit.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
