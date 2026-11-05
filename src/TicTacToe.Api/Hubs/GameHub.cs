using Cortex.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Games.Dtos;
using TicTacToe.Application.Games.Queries;

namespace TicTacToe.Api.Hubs;

/// <summary>
/// V1 hub: join/leave your own game room; everything else arrives as server pushes.
/// Push-only by design (ADR-0005) — the only state mutation is group membership.
/// </summary>
[Authorize]
public sealed class GameHub(IMediator mediator, ICurrentUser currentUser) : Hub
{
    public static string GroupName(Guid gameId) => $"game-{gameId}";

    public async Task JoinGameRoom(Guid gameId)
    {
        var game = await mediator.QueryAsync(new GetGameByIdQuery(gameId));
        if (game.IsFailure)
        {
            throw new HubException("Game not found.");
        }

        if (!IsParticipant(game.Value, currentUser.UserId))
        {
            throw new HubException("You are not a player in this game.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public Task LeaveGameRoom(Guid gameId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));

    private static bool IsParticipant(GameDto game, Guid? playerId)
        => playerId is { } id && (game.PlayerX.Id == id || game.PlayerO?.Id == id);
}
