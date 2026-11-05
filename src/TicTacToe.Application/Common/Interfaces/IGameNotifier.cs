using TicTacToe.Application.Games.Dtos;

namespace TicTacToe.Application.Common.Interfaces;

/// <summary>
/// Real-time push seam (V1 slice): game-room events only. V1.1 adds presence and lobby broadcasts
/// on this same interface — never broadcast from handlers directly.
/// </summary>
public interface IGameNotifier
{
    Task GameStartedAsync(GameDto game, CancellationToken cancellationToken = default);

    Task MoveMadeAsync(GameDto game, CancellationToken cancellationToken = default);

    Task GameFinishedAsync(GameDto game, CancellationToken cancellationToken = default);

    Task GameCancelledAsync(Guid gameId, CancellationToken cancellationToken = default);
}
