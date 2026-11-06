using Cortex.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicTacToe.Application.Common;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Application.Features.Games.Command;
using TicTacToe.Domain.Entities.GameAggregate;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Infrastructure.Hosting;

/// <summary>
/// The move-clock sweeper: every few seconds it finds InProgress games whose move deadline has
/// passed and ends them by timeout through the normal command pipeline (same validation, same
/// xmin guard as any user command). A player who leaves or disconnects simply runs out of time.
/// </summary>
public sealed class GameTimeoutService(
    IServiceScopeFactory scopeFactory,
    ILogger<GameTimeoutService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Timeout sweep failed; next scan in {Seconds}s", ScanInterval.TotalSeconds);
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IApplicationUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var expired = await unitOfWork.GameRepository.GetTimedOutGamesAsync(dateTimeProvider.UtcNow, cancellationToken);
        foreach (var game in expired)
        {
            if (game.Status == GameStatus.Waiting)
            {
                logger.LogInformation("Expiring waiting game {GameId} (nobody joined in time)", game.Id);
                await mediator.SendCommandAsync(new GameWaitingExpiredCommand(game.Id), cancellationToken);
            }
            else if (game.PlayerIdOfTurn is { } playerId)
            {
                logger.LogInformation("Timing out game {GameId} (player {PlayerId})", game.Id, playerId);
                await mediator.SendCommandAsync(new GameTimeoutCommand(game.Id, playerId), cancellationToken);
            }
        }
    }
}
