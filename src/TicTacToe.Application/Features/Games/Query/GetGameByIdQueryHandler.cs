using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Query;

public sealed class GetGameByIdQueryHandler(GameDtoLoader dtoLoader)
    : IQueryHandler<GetGameByIdQuery, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GetGameByIdQuery query, CancellationToken cancellationToken)
        => await dtoLoader.LoadByIdAsync(query.GameId, cancellationToken)
           ?? Result.Failure<GameDto>(new Error("Games.NotFound", "Game not found."));
}
