using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Domain.Players;
using TicTacToe.Infrastructure.Identity;

namespace TicTacToe.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Username).HasMaxLength(20).IsRequired();
            entity.HasIndex(p => p.Username).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
        });
    }
}
