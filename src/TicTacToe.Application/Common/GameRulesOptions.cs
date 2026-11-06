namespace TicTacToe.Application.Common;

/// <summary>Bound from "GameRules" configuration. The move clock: run out of time → loss by timeout.</summary>
public sealed class GameRulesOptions
{
    public int MoveTimeoutSeconds { get; set; } = 60;

    public TimeSpan MoveTimeout => TimeSpan.FromSeconds(MoveTimeoutSeconds);
}
