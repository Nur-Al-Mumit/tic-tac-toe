using TicTacToe.Domain.Exceptions;

namespace TicTacToe.Application.Common.Results;

/// <summary>Maps the six expected domain exceptions to Result errors (codes per api-spec §1.4).</summary>
public static class DomainErrors
{
    public static Error? ToError(Exception exception) => exception switch
    {
        GameFullException => new Error("Games.Full", exception.Message),
        NotYourTurnException => new Error("Games.NotYourTurn", exception.Message),
        CellOccupiedException => new Error("Games.CellOccupied", exception.Message),
        InvalidPositionException => new Error("Games.InvalidPosition", exception.Message),
        GameNotInProgressException => new Error("Games.NotInProgress", exception.Message),
        CannotJoinOwnGameException => new Error("Games.CannotJoinOwnGame", exception.Message),
        _ => null,
    };
}
