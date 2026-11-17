using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_venue_id",
                table: "bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "bookings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "cancelled_by_venue",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_staff_id",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guest_name",
                table: "bookings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Booking");

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "bookings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "App");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_venue_id_starts_at",
                table: "bookings",
                columns: new[] { "venue_id", "starts_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings",
                sql: "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR (kind = 'Booking' AND (customer_id IS NULL) <> (guest_name IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_venue_id_starts_at",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cancelled_by_venue",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "created_by_staff_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "guest_name",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "note",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "source",
                table: "bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_venue_id",
                table: "bookings",
                column: "venue_id");
        }
    }
}
