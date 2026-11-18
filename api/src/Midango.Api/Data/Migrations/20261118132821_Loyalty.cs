using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Loyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gold_games",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "gold_multiplier",
                table: "platform_settings",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "max_redeem_percent",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "points_per_redeem_step",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "points_per_sar",
                table: "platform_settings",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "sar_per_redeem_step",
                table: "platform_settings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "silver_games",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "silver_multiplier",
                table: "platform_settings",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "tier_window_days",
                table: "platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "points_discount",
                table: "match_players",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "points_redeemed",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "points_discount",
                table: "bookings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "points_redeemed",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "points_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_points_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_points_entries_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_points_entries_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "platform_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "gold_games", "gold_multiplier", "max_redeem_percent", "points_per_redeem_step", "points_per_sar", "sar_per_redeem_step", "silver_games", "silver_multiplier", "tier_window_days" },
                values: new object[] { 25, 1.5m, 50, 100, 1m, 10m, 10, 1.25m, 90 });

            migrationBuilder.CreateIndex(
                name: "ix_points_entries_booking_id",
                table: "points_entries",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_points_entries_customer_id_at",
                table: "points_entries",
                columns: new[] { "customer_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "points_entries");

            migrationBuilder.DropColumn(
                name: "gold_games",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "gold_multiplier",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "max_redeem_percent",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "points_per_redeem_step",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "points_per_sar",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "sar_per_redeem_step",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "silver_games",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "silver_multiplier",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "tier_window_days",
                table: "platform_settings");

            migrationBuilder.DropColumn(
                name: "points_discount",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "points_redeemed",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "points_discount",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "points_redeemed",
                table: "bookings");
        }
    }
}
