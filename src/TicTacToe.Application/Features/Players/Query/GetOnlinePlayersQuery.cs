using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Players.Dtos;

namespace TicTacToe.Application.Features.Players.Query;

public sealed record GetOnlinePlayersQuery : IQuery<Result<IReadOnlyList<OnlinePlayerDto>>>;
