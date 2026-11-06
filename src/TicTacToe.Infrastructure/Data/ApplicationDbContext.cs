using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Domain.Entities;
using TicTacToe.Domain.Entities.GameAggregate;
using TicTacToe.Infrastructure.Identity;

namespace TicTacToe.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<Move> Moves => Set<Move>();

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

        builder.Entity<Game>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.Property(g => g.PlayerXId).IsRequired();
            entity.Property(g => g.PlayerOId);
            entity.Property(g => g.Status).HasConversion<int>();
            entity.Property(g => g.CurrentTurn).HasConversion<int>();

            // Result value object → owned columns on games (result_outcome, result_reason, result_winning_line).
            entity.OwnsOne(g => g.Result, result =>
            {
                result.Property(r => r.Outcome).HasConversion<int>().HasColumnName("result_outcome");
                result.Property(r => r.Reason).HasConversion<int>().HasColumnName("result_reason");
                result.Property(r => r.WinningLine).HasColumnName("result_winning_line");
            });

            // Moves child collection via the private backing field; cascade delete from the aggregate root.
            entity.Metadata.FindNavigation(nameof(Game.Moves))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(g => g.Moves)
                .WithOne()
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(g => g.MoveDeadlineUtc);
            // Timeout sweeper lookup: InProgress games with an expired move clock.
            entity.HasIndex(g => g.MoveDeadlineUtc)
                .HasDatabaseName("ix_games_move_deadline_active")
                .HasFilter("status IN (0, 1)");

            // xmin as the optimistic concurrency token (ADR-0006). Npgsql maps a uint rowversion
            // property to the real PostgreSQL xmin system column and reads it back after each save.
            entity.Property(g => g.Version).IsRowVersion();

            // Lobby query index: games(status, created_at_utc DESC).
            entity.HasIndex(g => new { g.Status, g.CreatedAtUtc })
                .HasDatabaseName("ix_games_status_created_at_utc_desc")
                .IsDescending(false, true);

            // One active (Waiting/InProgress) game per player, enforced at the storage level (FR-07).
            entity.HasIndex(g => g.PlayerXId)
                .HasDatabaseName("ix_games_player_x_active_unique")
                .HasFilter("status IN (0, 1)")
                .IsUnique();
            entity.HasIndex(g => g.PlayerOId)
                .HasDatabaseName("ix_games_player_o_active_unique")
                .HasFilter("status = 1")
                .IsUnique();
        });

        builder.Entity<Move>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.ToTable(t => t.HasCheckConstraint("ck_moves_position_range", "position BETWEEN 0 AND 8"));

            // Last line of defense against double moves, even under a race (concurrency §7).
            entity.HasIndex(m => new { m.GameId, m.Position }).IsUnique();
            entity.HasIndex(m => new { m.GameId, m.MoveNumber }).IsUnique();
        });
    }
}
