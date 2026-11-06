using Cortex.Mediator.Queries;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Query;

/// <summary>Lobby list; status filter "waiting"/"inprogress" (V1 lobby uses waiting only).</summary>
public sealed record GetAllGamesQuery(string? Status = null) : IQuery<Result<IReadOnlyList<LobbyGameDto>>>;
