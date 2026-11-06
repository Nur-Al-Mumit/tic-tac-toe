namespace TicTacToe.Application.Features.Auth.Dtos;

/// <summary>GET-shaped user info returned inside AuthResponse and /auth/me.</summary>
public sealed record UserDto(Guid Id, string Username, string Email);

/// <summary>api-spec §3: { accessToken, refreshToken, expiresAtUtc, user }.</summary>
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);
