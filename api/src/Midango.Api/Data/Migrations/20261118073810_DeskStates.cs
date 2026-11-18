using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeskStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_collected",
                table: "bookings",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_in_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "checked_in_by_staff_id",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "no_show_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_checked_in_confirmed",
                table: "bookings",
                sql: "checked_in_at IS NULL OR (status = 'Confirmed' AND kind = 'Booking' AND cash_collected IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_checked_in_confirmed",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cash_collected",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "checked_in_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "checked_in_by_staff_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "no_show_at",
                table: "bookings");
        }
    }
}
