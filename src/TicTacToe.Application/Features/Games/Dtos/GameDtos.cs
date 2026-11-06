using TicTacToe.Domain.Entities.GameAggregate;

namespace TicTacToe.Application.Features.Games.Dtos;

public sealed record PlayerRef(Guid Id, string Username);

public sealed record GameResultDto(string Outcome, string Reason, int[]? WinningLine);

/// <summary>api-spec §3: { id, status, playerX, playerO, board[9], currentTurn, result, createdAtUtc }.</summary>
public sealed record GameDto(
    Guid Id,
    string Status,
    PlayerRef PlayerX,
    PlayerRef? PlayerO,
    string?[] Board,
    string CurrentTurn,
    GameResultDto? Result,
    DateTime CreatedAtUtc,
    DateTime? MoveDeadlineUtc = null);

public sealed record LobbyGameDto(
    Guid Id,
    string Status,
    PlayerRef PlayerX,
    PlayerRef? PlayerO,
    DateTime CreatedAtUtc);

public static class GameMappings
{
    /// <summary>Board is derived from moves — never stored (PRD §7.2). Enum names are the wire format.</summary>
    public static GameDto ToGameDto(this Game game, string playerXName, string? playerOName)
    {
        var board = new string?[9];
        foreach (var move in game.Moves)
        {
            board[move.Position] = move.Mark.ToString();
        }

        return new GameDto(
            game.Id,
            game.Status.ToString(),
            new PlayerRef(game.PlayerXId, playerXName),
            game.PlayerOId is { } oId ? new PlayerRef(oId, playerOName ?? "?") : null,
            board,
            game.CurrentTurn.ToString(),
            game.Result is { } result
                ? new GameResultDto(result.Outcome.ToString(), result.Reason.ToString(), result.WinningLine)
                : null,
            game.CreatedAtUtc,
            game.MoveDeadlineUtc);
    }
}
