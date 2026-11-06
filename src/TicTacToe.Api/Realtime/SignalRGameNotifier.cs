using Microsoft.AspNetCore.SignalR;
using TicTacToe.Api.Hubs;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Api.Realtime;

/// <summary>Delivers IGameNotifier pushes to the game-{id} SignalR group (fire-and-forget from the hub's perspective).</summary>
public sealed class SignalRGameNotifier(IHubContext<GameHub> hubContext) : IGameNotifier
{
    public Task GameStartedAsync(GameDto game, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.GroupName(game.Id)).SendAsync("GameStarted", game, cancellationToken);

    public Task MoveMadeAsync(GameDto game, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.GroupName(game.Id)).SendAsync("MoveMade", game, cancellationToken);

    public Task GameFinishedAsync(GameDto game, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.GroupName(game.Id)).SendAsync("GameFinished", game, cancellationToken);

    public Task GameCancelledAsync(Guid gameId, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.GroupName(gameId)).SendAsync("GameCancelled", new { gameId }, cancellationToken);

    public Task LobbyGameAddedAsync(GameDto game, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.LobbyGroupName).SendAsync("GameCreated", game, cancellationToken);

    public Task LobbyGameRemovedAsync(Guid gameId, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(GameHub.LobbyGroupName).SendAsync("GameRemoved", new { gameId }, cancellationToken);
}
