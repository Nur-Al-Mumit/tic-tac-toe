using Cortex.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Common;
using TicTacToe.Application.Features.Players.Dtos;
using TicTacToe.Application.Features.Players.Query;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/v1/players")]
[Authorize]
public sealed class PlayersController(IMediator mediator) : ControllerBase
{
    /// <summary>Who is online right now, with Available / In game status (US-04).</summary>
    [HttpGet("online")]
    [ProducesResponseType<IReadOnlyList<OnlinePlayerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Online(CancellationToken cancellationToken)
        => this.FromResult(await mediator.QueryAsync(new GetOnlinePlayersQuery(), cancellationToken));
}
