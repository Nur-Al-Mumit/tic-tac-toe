using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicTacToe.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGamesAndMoves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_x_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_o_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_turn = table.Column<int>(type: "integer", nullable: false),
                    result_outcome = table.Column<int>(type: "integer", nullable: true),
                    result_reason = table.Column<int>(type: "integer", nullable: true),
                    result_winning_line = table.Column<int[]>(type: "integer[]", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "moves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    move_number = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    mark = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    played_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moves", x => x.id);
                    table.CheckConstraint("ck_moves_position_range", "position BETWEEN 0 AND 8");
                    table.ForeignKey(
                        name: "fk_moves_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_games_player_o_active_unique",
                table: "games",
                column: "player_o_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_games_player_x_active_unique",
                table: "games",
                column: "player_x_id",
                unique: true,
                filter: "status IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_games_status_created_at_utc_desc",
                table: "games",
                columns: new[] { "status", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_moves_game_id_move_number",
                table: "moves",
                columns: new[] { "game_id", "move_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moves_game_id_position",
                table: "moves",
                columns: new[] { "game_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moves");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
