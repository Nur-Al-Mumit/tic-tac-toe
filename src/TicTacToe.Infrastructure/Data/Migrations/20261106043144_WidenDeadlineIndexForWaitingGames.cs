using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicTacToe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WidenDeadlineIndexForWaitingGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_games_move_deadline_active",
                table: "games");

            migrationBuilder.CreateIndex(
                name: "ix_games_move_deadline_active",
                table: "games",
                column: "move_deadline_utc",
                filter: "status IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_games_move_deadline_active",
                table: "games");

            migrationBuilder.CreateIndex(
                name: "ix_games_move_deadline_active",
                table: "games",
                column: "move_deadline_utc",
                filter: "status = 1");
        }
    }
}
