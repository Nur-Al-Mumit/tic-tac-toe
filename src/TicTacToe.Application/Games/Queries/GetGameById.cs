using Cortex.Mediator.Queries;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Games.Dtos;

namespace TicTacToe.Application.Games.Queries;

public sealed record GetGameByIdQuery(Guid GameId) : IQuery<Result<GameDto>>;

public sealed class GetGameByIdQueryValidator : AbstractValidator<GetGameByIdQuery>
{
    public GetGameByIdQueryValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}

public sealed class GetGameByIdQueryHandler(GameDtoLoader dtoLoader)
    : IQueryHandler<GetGameByIdQuery, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GetGameByIdQuery query, CancellationToken cancellationToken)
        => await dtoLoader.LoadByIdAsync(query.GameId, cancellationToken)
           ?? Result.Failure<GameDto>(new Error("Games.NotFound", "Game not found."));
}
