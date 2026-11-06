namespace TicTacToe.Domain.Entities.GameAggregate;

/// <summary>Set exactly once, when the game finishes (PRD §7.2 Result value object).</summary>
public sealed record GameResult(GameOutcome Outcome, FinishReason Reason, int[]? WinningLine);
