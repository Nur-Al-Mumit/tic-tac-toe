using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Query;

/// <summary>US-13: the resync source after a page refresh or hub reconnect — 204 when the player has no active game.</summary>
public sealed record GetMyActiveGameQuery : IQuery<Result<GameDto?>>;
