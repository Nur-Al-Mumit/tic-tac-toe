using Cortex.Mediator.Queries;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Query;

public sealed record GetGameByIdQuery(Guid GameId) : IQuery<Result<GameDto>>;

public sealed class GetGameByIdQueryValidator : AbstractValidator<GetGameByIdQuery>
{
    public GetGameByIdQueryValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}
