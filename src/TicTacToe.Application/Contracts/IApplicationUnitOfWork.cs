using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Domain.Contracts;

namespace TicTacToe.Application.Contracts;

public interface IApplicationUnitOfWork : IUnitOfWork
{
    IGameRepository GameRepository { get; }

    IPlayerProfileRepository PlayerProfileRepository { get; }
}
