namespace TicTacToe.Domain.Contracts;

/// <summary>Implemented by aggregates that collect domain events for post-commit dispatch.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
