using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tournament_id",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tournaments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    team_size = table.Column<int>(type: "integer", nullable: false),
                    max_teams = table.Column<int>(type: "integer", nullable: false),
                    entry_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registration_closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    court_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournaments", x => x.id);
                    table.CheckConstraint("ck_tournaments_ends_after_start", "ends_at > starts_at");
                    table.CheckConstraint("ck_tournaments_registration_closes", "registration_closes_at <= starts_at");
                    table.ForeignKey(
                        name: "fk_tournaments_sport_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "sport_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournaments_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tournament_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tournament_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    captain_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cash_collected = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_tournament_teams_customers_captain_customer_id",
                        column: x => x.captain_customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_teams_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournament_team_member",
                columns: table => new
                {
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_team_member", x => new { x.team_id, x.position });
                    table.ForeignKey(
                        name: "fk_tournament_team_member_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tournament_team_member_tournament_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_tournament_id",
                table: "bookings",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_team_member_customer_id",
                table: "tournament_team_member",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_captain_customer_id",
                table: "tournament_teams",
                column: "captain_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_tournament_id_registered_at",
                table: "tournament_teams",
                columns: new[] { "tournament_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_category_id",
                table: "tournaments",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_status_starts_at",
                table: "tournaments",
                columns: new[] { "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_venue_id_created_at",
                table: "tournaments",
                columns: new[] { "venue_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_bookings_tournaments_tournament_id",
                table: "bookings",
                column: "tournament_id",
                principalTable: "tournaments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bookings_tournaments_tournament_id",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "tournament_team_member");

            migrationBuilder.DropTable(
                name: "tournament_teams");

            migrationBuilder.DropTable(
                name: "tournaments");

            migrationBuilder.DropIndex(
                name: "ix_bookings_tournament_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "tournament_id",
                table: "bookings");
        }
    }
}
