using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Contracts.Services;

/// <summary>
/// Real-time push seam: game-room events plus lobby list updates. Presence (online players)
/// stays a post-V1 addition on this same interface; handlers never broadcast directly.
/// </summary>
public interface IGameNotifier
{
    Task GameStartedAsync(GameDto game, CancellationToken cancellationToken = default);

    Task MoveMadeAsync(GameDto game, CancellationToken cancellationToken = default);

    Task GameFinishedAsync(GameDto game, CancellationToken cancellationToken = default);

    Task GameCancelledAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>A new waiting game appeared in the lobby.</summary>
    Task LobbyGameAddedAsync(GameDto game, CancellationToken cancellationToken = default);

    /// <summary>A game left the joinable list (started, cancelled or finished).</summary>
    Task LobbyGameRemovedAsync(Guid gameId, CancellationToken cancellationToken = default);
}
