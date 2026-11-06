namespace TicTacToe.Domain.Contracts;

public interface IUnitOfWork
{
    void Save();

    Task SaveAsync();
}
