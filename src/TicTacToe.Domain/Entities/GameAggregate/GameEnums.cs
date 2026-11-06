namespace TicTacToe.Domain.Entities.GameAggregate;

public enum Mark
{
    X = 0,
    O = 1,
}

public enum GameStatus
{
    Waiting = 0,
    InProgress = 1,
    Finished = 2,
    Cancelled = 3,
}

public enum GameOutcome
{
    XWins = 0,
    OWins = 1,
    Draw = 2,
}

public enum FinishReason
{
    Normal = 0,
    Forfeit = 1,
}
