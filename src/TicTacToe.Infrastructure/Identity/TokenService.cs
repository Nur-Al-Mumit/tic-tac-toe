using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Infrastructure.Persistence;

namespace TicTacToe.Infrastructure.Identity;

/// <summary>15-minute JWTs; 7-day refresh tokens, hashed at rest, rotated on every use with family revocation on reuse.</summary>
public class TokenService(AppDbContext context, IDateTimeProvider dateTimeProvider, IConfiguration configuration) : ITokenService
{
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
    private readonly string _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
    private readonly TimeSpan _accessTokenLifetime = TimeSpan.FromMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));
    private readonly TimeSpan _refreshTokenLifetime = TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenDays", 7));

    public async Task<AuthResponse> IssueTokensAsync(Guid userId, string username, string email, string? createdByIp, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var expiresAtUtc = now.Add(_accessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("name", username),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var secret = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters (256 bits) for HS256.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refresh = await CreateRefreshTokenAsync(userId, createdByIp, cancellationToken);

        return new AuthResponse(accessToken, refresh, expiresAtUtc, new UserDto(userId, username, email));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(string refreshToken, string? createdByIp, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var stored = await context.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || !stored.IsActive)
        {
            // Reuse of a revoked token: revoke every active token for the user ("revoke the family").
            if (stored is { RevokedAtUtc: not null })
            {
                await RevokeFamilyAsync(stored.UserId, cancellationToken);
            }

            return Result.Failure<AuthResponse>(Error.InvalidRefreshToken());
        }

        var user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == stored.UserId, cancellationToken);
        var response = await IssueTokensAsync(user.Id, user.UserName!, user.Email!, createdByIp, cancellationToken);

        stored.RevokedAtUtc = dateTimeProvider.UtcNow;
        stored.ReplacedByTokenHash = Hash(response.RefreshToken);

        return response;
    }

    public async Task RevokeAsync(string refreshToken, Guid ownerId, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var stored = await context.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == ownerId, cancellationToken);

        // Unknown or foreign token: silent no-op (no enumeration through logout).
        if (stored is { IsActive: true })
        {
            stored.RevokedAtUtc = dateTimeProvider.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, string? createdByIp, CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // 256-bit
        context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAtUtc = dateTimeProvider.UtcNow.Add(_refreshTokenLifetime),
            CreatedAtUtc = dateTimeProvider.UtcNow,
            CreatedByIp = createdByIp,
        });
        await context.SaveChangesAsync(cancellationToken);
        return raw;
    }

    private async Task RevokeFamilyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await context.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAtUtc = dateTimeProvider.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
