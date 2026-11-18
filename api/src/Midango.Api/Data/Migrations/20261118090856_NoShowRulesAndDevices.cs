using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoShowRulesAndDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reminder_sent_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_devices_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    reminder_hours_before = table.Column<int>(type: "integer", nullable: false),
                    no_show_limit = table.Column<int>(type: "integer", nullable: false),
                    no_show_window_days = table.Column<int>(type: "integer", nullable: false),
                    limited_upcoming_bookings = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_settings", x => x.id);
                    table.CheckConstraint("ck_platform_settings_singleton", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "platform_settings",
                columns: new[] { "id", "limited_upcoming_bookings", "no_show_limit", "no_show_window_days", "reminder_hours_before" },
                values: new object[] { 1, 1, 2, 30, 3 });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status_starts_at",
                table: "bookings",
                columns: new[] { "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_devices_customer_id",
                table: "customer_devices",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_devices_token",
                table: "customer_devices",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_devices");

            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_status_starts_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "reminder_sent_at",
                table: "bookings");
        }
    }
}
