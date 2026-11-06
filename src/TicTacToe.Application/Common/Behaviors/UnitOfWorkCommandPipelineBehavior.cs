using Cortex.Mediator.Commands;
using TicTacToe.Application.Contracts;

namespace TicTacToe.Application.Common.Behaviors;

/// <summary>
/// Commits command-side changes after the handler succeeds, then domain events dispatch post-commit
/// inside IUnitOfWork. Queries never pass through here (command pipeline only).
/// </summary>
public sealed class UnitOfWorkCommandPipelineBehavior<TCommand, TResponse>(IApplicationUnitOfWork unitOfWork)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CommandHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        await unitOfWork.SaveAsync();
        return response;
    }
}
