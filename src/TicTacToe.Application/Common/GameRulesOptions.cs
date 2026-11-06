namespace TicTacToe.Application.Common;

/// <summary>Bound from "GameRules" configuration. Two clocks: the waiting clock (nobody joins → game
/// auto-cancels, freeing the host) and the move clock (run out of time → loss by timeout).</summary>
public sealed class GameRulesOptions
{
    public int MoveTimeoutSeconds { get; set; } = 60;

    public int WaitingTimeoutSeconds { get; set; } = 120;

    public TimeSpan MoveTimeout => TimeSpan.FromSeconds(MoveTimeoutSeconds);

    public TimeSpan WaitingTimeout => TimeSpan.FromSeconds(WaitingTimeoutSeconds);
}
