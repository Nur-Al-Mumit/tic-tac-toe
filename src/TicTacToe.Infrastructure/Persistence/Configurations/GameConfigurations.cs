using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicTacToe.Domain.Games;

namespace TicTacToe.Infrastructure.Persistence.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.PlayerXId).IsRequired();
        builder.Property(g => g.PlayerOId);
        builder.Property(g => g.Status).HasConversion<int>();
        builder.Property(g => g.CurrentTurn).HasConversion<int>();

        // Result value object → owned columns on games (result_outcome, result_reason, result_winning_line).
        builder.OwnsOne(g => g.Result, result =>
        {
            result.Property(r => r.Outcome).HasConversion<int>().HasColumnName("result_outcome");
            result.Property(r => r.Reason).HasConversion<int>().HasColumnName("result_reason");
            result.Property(r => r.WinningLine).HasColumnName("result_winning_line");
        });

        // Moves child collection via the private backing field; cascade delete from the aggregate root.
        builder.Metadata.FindNavigation(nameof(Game.Moves))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(g => g.Moves)
            .WithOne()
            .HasForeignKey(m => m.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // xmin as the optimistic concurrency token (ADR-0006).
        builder.Property(g => g.Version).IsRowVersion();

        // Lobby query index: games(status, created_at_utc DESC).
        builder.HasIndex(g => new { g.Status, g.CreatedAtUtc })
            .HasDatabaseName("ix_games_status_created_at_utc_desc")
            .IsDescending(false, true);

        // One active (Waiting/InProgress) game per player, enforced at the storage level (FR-07).
        builder.HasIndex(g => g.PlayerXId)
            .HasDatabaseName("ix_games_player_x_active_unique")
            .HasFilter("status IN (0, 1)")
            .IsUnique();
        builder.HasIndex(g => g.PlayerOId)
            .HasDatabaseName("ix_games_player_o_active_unique")
            .HasFilter("status = 1")
            .IsUnique();
    }
}

public class MoveConfiguration : IEntityTypeConfiguration<Move>
{
    public void Configure(EntityTypeBuilder<Move> builder)
    {
        builder.HasKey(m => m.Id);
        builder.ToTable(t => t.HasCheckConstraint("ck_moves_position_range", "position BETWEEN 0 AND 8"));

        // Last line of defense against double moves, even under a race (concurrency §7).
        builder.HasIndex(m => new { m.GameId, m.Position }).IsUnique();
        builder.HasIndex(m => new { m.GameId, m.MoveNumber }).IsUnique();
    }
}
