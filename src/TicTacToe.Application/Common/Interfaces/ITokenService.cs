using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.Common.Interfaces;

public interface ITokenService
{
    /// <summary>Issues a 15-minute access token and a new 7-day rotating refresh token.</summary>
    Task<AuthResponse> IssueTokensAsync(Guid userId, string username, string email, string? createdByIp, CancellationToken cancellationToken);

    /// <summary>Rotates a refresh token; reuse of a revoked token revokes the family.</summary>
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, string? createdByIp, CancellationToken cancellationToken);

    /// <summary>Revokes a refresh token owned by the given user; foreign tokens are a silent no-op (no enumeration).</summary>
    Task RevokeAsync(string refreshToken, Guid ownerId, CancellationToken cancellationToken);
}
