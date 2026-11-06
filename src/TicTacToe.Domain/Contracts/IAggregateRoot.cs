namespace TicTacToe.Domain.Contracts;

public interface IAggregateRoot<TKey>
{
    TKey Id { get; set; }
}
