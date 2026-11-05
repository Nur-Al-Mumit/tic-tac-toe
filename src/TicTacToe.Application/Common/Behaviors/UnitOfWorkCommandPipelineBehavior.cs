using Cortex.Mediator.Commands;
using TicTacToe.Application.Common.Interfaces;

namespace TicTacToe.Application.Common.Behaviors;

/// <summary>
/// Commits command-side changes after the handler succeeds, then domain events dispatch post-commit
/// inside IUnitOfWork. Queries never pass through here (command pipeline only).
/// </summary>
public sealed class UnitOfWorkCommandPipelineBehavior<TCommand, TResponse>(IUnitOfWork unitOfWork)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, CommandHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }
}
