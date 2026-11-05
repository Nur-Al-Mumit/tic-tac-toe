namespace TicTacToe.Application.Common.Interfaces;

/// <summary>The authenticated caller's identity — always from the JWT, never from a payload.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Username { get; }
}
