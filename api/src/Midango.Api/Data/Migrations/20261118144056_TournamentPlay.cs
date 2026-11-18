using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TournamentPlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "champion_team_id",
                table: "tournaments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "drawn_at",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "group_count",
                table: "tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "match_minutes",
                table: "tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "draw_position",
                table: "tournament_teams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "group_no",
                table: "tournament_teams",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tournament_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tournament_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    group_no = table.Column<int>(type: "integer", nullable: true),
                    team_a_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_b_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score_a = table.Column<int>(type: "integer", nullable: true),
                    score_b = table.Column<int>(type: "integer", nullable: true),
                    winner_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_match_id = table.Column<Guid>(type: "uuid", nullable: true),
                    next_slot = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    is_bye = table.Column<bool>(type: "boolean", nullable: false),
                    result_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_tournament_matches_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_matches_tournament_matches_next_match_id",
                        column: x => x.next_match_id,
                        principalTable: "tournament_matches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_tournament_matches_tournament_teams_team_a_id",
                        column: x => x.team_a_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_matches_tournament_teams_team_b_id",
                        column: x => x.team_b_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_matches_tournament_teams_winner_team_id",
                        column: x => x.winner_team_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_matches_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_court_id",
                table: "tournament_matches",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_next_match_id",
                table: "tournament_matches",
                column: "next_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_team_a_id",
                table: "tournament_matches",
                column: "team_a_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_team_b_id",
                table: "tournament_matches",
                column: "team_b_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_tournament_id_round_position",
                table: "tournament_matches",
                columns: new[] { "tournament_id", "round", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_tournament_matches_winner_team_id",
                table: "tournament_matches",
                column: "winner_team_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_matches");

            migrationBuilder.DropColumn(
                name: "champion_team_id",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "drawn_at",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "group_count",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "match_minutes",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "draw_position",
                table: "tournament_teams");

            migrationBuilder.DropColumn(
                name: "group_no",
                table: "tournament_teams");
        }
    }
}
