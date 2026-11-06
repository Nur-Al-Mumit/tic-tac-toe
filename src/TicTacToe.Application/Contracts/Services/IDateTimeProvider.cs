namespace TicTacToe.Application.Contracts.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
