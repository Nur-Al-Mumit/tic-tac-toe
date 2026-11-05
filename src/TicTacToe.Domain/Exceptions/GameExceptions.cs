namespace TicTacToe.Domain.Exceptions;

/// <summary>Expected domain rule violations — mapped to Result failures / 4xx at the boundary (PRD §7.5).</summary>
public class GameFullException() : Exception("Game is already full.");

public class NotYourTurnException() : Exception("It is not your turn.");

public class CellOccupiedException(int position) : Exception($"Cell {position} is already taken.");

public class InvalidPositionException(int position) : Exception($"Position must be 0-8, got {position}.");

public class GameNotInProgressException() : Exception("The game is not in progress.");

public class CannotJoinOwnGameException() : Exception("You cannot join your own game.");
