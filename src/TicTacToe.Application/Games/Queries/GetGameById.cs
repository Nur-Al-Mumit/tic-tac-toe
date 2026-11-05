using Cortex.Mediator.Queries;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Common.Interfaces;
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

public sealed class GetGameByIdQueryHandler(IReadDbContext readDbContext, GameDtoLoader dtoLoader)
    : IQueryHandler<GetGameByIdQuery, Result<GameDto>>
{
    public async Task<Result<GameDto>> Handle(GetGameByIdQuery query, CancellationToken cancellationToken)
    {
        var game = await readDbContext.Games
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(g => g.Id == query.GameId, cancellationToken);

        return game is null
            ? Result.Failure<GameDto>(new Error("Games.NotFound", "Game not found."))
            : await dtoLoader.LoadAsync(game, cancellationToken);
    }
}
