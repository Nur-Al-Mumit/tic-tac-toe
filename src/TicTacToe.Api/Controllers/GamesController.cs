using Cortex.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using TicTacToe.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Common;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Command;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Application.Features.Games.Query;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/v1/games")]
[Authorize]
public sealed class GamesController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create a game as Player X (201). 409 when you already have an active game.</summary>
    [HttpPost]
    [ProducesResponseType<GameDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(new GameCreateCommand(), cancellationToken), StatusCodes.Status201Created);

    /// <summary>Lobby list; ?status=waiting|inprogress (200).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LobbyGameDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
        => this.FromResult(await mediator.QueryAsync(new GetAllGamesQuery(status), cancellationToken));

    /// <summary>Game detail with derived board (200/404).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => this.FromResult(await mediator.QueryAsync(new GetGameByIdQuery(id), cancellationToken));

    /// <summary>My active (Waiting/InProgress) game — the resync source (200, or 204 when none).</summary>
    [HttpGet("active")]
    [ProducesResponseType<GameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Active(CancellationToken cancellationToken)
        => this.FromResult(
            await mediator.QueryAsync(new GetMyActiveGameQuery(), cancellationToken),
            StatusCodes.Status200OK,
            StatusCodes.Status204NoContent);

    /// <summary>Join a waiting game as Player O (200). 409 when full/own game/busy.</summary>
    [HttpPost("{id:guid}/join")]
    [ProducesResponseType<GameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Join(Guid id, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(new GameJoinCommand(id), cancellationToken));

    /// <summary>Place a mark (200). 409 not your turn / 422 invalid position or occupied cell.</summary>
    [HttpPost("{id:guid}/moves")]
    [EnableRateLimiting(RateLimitingExtensions.MovesPolicy)]
    [ProducesResponseType<GameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Move(Guid id, MoveGameRequest request, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(new GameMoveCommand(id, request.Position), cancellationToken));

    /// <summary>Host cancels a waiting game (204).</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(new GameCancelCommand(id), cancellationToken));

    /// <summary>Leave / forfeit — the leaver loses (204).</summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(
            new GameLeaveCommand(id, currentUser.UserId ?? Guid.Empty), cancellationToken));
}

public sealed record MoveGameRequest(int Position);
