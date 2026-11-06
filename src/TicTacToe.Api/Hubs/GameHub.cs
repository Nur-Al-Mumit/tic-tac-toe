using Cortex.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;
using TicTacToe.Application.Features.Games.Query;
using TicTacToe.Application.Features.Players.Dtos;
using TicTacToe.Application.Features.Players.Query;
using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Api.Hubs;

/// <summary>
/// The hub: everyone joins the lobby group (live list + presence); players join their game room.
/// Push-only by design (ADR-0005) — the connection lifecycle (presence) is the sole state exception.
/// </summary>
[Authorize]
public sealed class GameHub(
    IMediator mediator,
    ICurrentUser currentUser,
    IApplicationUnitOfWork unitOfWork,
    IPresenceTracker presenceTracker) : Hub
{
    public const string LobbyGroupName = "lobby";

    public static string GroupName(Guid gameId) => $"game-{gameId}";

    public override async Task OnConnectedAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return;
        }

        presenceTracker.TrackConnection(userId, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroupName);

        // Back with a running game? Rejoin the room and tell the opponent we are here.
        var active = await unitOfWork.GameRepository.GetActiveGameForPlayerAsync(userId, Context.ConnectionAborted);
        if (active is { Status: GameStatus.InProgress })
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(active.Id));
            await Clients.Group(GroupName(active.Id))
                .SendAsync("OpponentConnection", new { playerId = userId, connected = true }, Context.ConnectionAborted);
        }

        await BroadcastPresenceAsync(Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (currentUser.UserId is not { } userId)
        {
            return;
        }

        var wentOffline = presenceTracker.RemoveConnection(userId, Context.ConnectionId);
        if (wentOffline)
        {
            var active = await unitOfWork.GameRepository.GetActiveGameForPlayerAsync(userId, Context.ConnectionAborted);
            if (active is { Status: GameStatus.InProgress })
            {
                // The move clock will finish the game if they never come back; meanwhile hint the opponent.
                await Clients.Group(GroupName(active.Id))
                    .SendAsync("OpponentConnection", new { playerId = userId, connected = false }, Context.ConnectionAborted);
            }

            await BroadcastPresenceAsync(Context.ConnectionAborted);
        }

        await base.OnDisconnectedAsync(exception);
    }

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

    private async Task BroadcastPresenceAsync(CancellationToken cancellationToken)
    {
        var players = await mediator.QueryAsync(new GetOnlinePlayersQuery(), cancellationToken);
        if (players.IsSuccess)
        {
            await Clients.Group(LobbyGroupName).SendAsync(
                "PresenceChanged",
                new { onlineCount = players.Value.Count, players = players.Value },
                cancellationToken);
        }
    }

    private static bool IsParticipant(GameDto game, Guid? playerId)
        => playerId is { } id && (game.PlayerX.Id == id || game.PlayerO?.Id == id);
}
