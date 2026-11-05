using Cortex.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Common;
using TicTacToe.Application.Auth.Commands;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Interfaces;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Register with username, email and password; returns tokens (201).</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterUserCommand command, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(command, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Login by email or username (200).</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(command, cancellationToken));

    /// <summary>Rotate the refresh token (200).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(command, cancellationToken));

    /// <summary>Revoke the refresh token (204).</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
        => this.FromResult(await mediator.SendAsync(command, cancellationToken));

    /// <summary>Current authenticated user (200).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
        => currentUser.UserId is { } id && currentUser.Username is { } username
            ? Ok(new { id, username })
            : Unauthorized();
}
