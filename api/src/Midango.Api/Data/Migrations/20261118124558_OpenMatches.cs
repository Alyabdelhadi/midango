using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OpenMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "players_per_match",
                table: "sport_categories",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<bool>(
                name: "is_open_match",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "match_level",
                table: "bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_players",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_levels",
                columns: table => new
                {
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_levels", x => new { x.customer_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_customer_levels_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_levels_sport_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "sport_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    late_leave = table.Column<bool>(type: "boolean", nullable: false),
                    removed_by_organiser = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cash_collected = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    no_show_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_players", x => new { x.booking_id, x.customer_id });
                    table.ForeignKey(
                        name: "fk_match_players_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_players_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sport_categories_players_per_match",
                table: "sport_categories",
                sql: "players_per_match BETWEEN 2 AND 22");

            migrationBuilder.CreateIndex(
                name: "ix_customer_levels_category_id",
                table: "customer_levels",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_customer_id",
                table: "match_players",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_levels");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sport_categories_players_per_match",
                table: "sport_categories");

            migrationBuilder.DropColumn(
                name: "players_per_match",
                table: "sport_categories");

            migrationBuilder.DropColumn(
                name: "is_open_match",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "match_level",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "max_players",
                table: "bookings");
        }
    }
}
