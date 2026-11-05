namespace TicTacToe.Infrastructure.Identity;

/// <summary>Rotating refresh token; only the SHA-256 hash is stored (docs/02-backend/auth-identity-jwt.md).</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsActive => RevokedAtUtc is null && !IsExpired;
}
