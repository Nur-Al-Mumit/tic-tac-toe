namespace TicTacToe.Application.Features.Players.Dtos;

/// <summary>An online player as shown in the lobby panel (US-04).</summary>
public sealed record OnlinePlayerDto(Guid Id, string Username, bool InGame);
