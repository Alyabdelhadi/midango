using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffBookingNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings",
                sql: "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR (kind = 'Booking' AND (customer_id IS NOT NULL OR guest_name IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_party",
                table: "bookings",
                sql: "(kind = 'Block' AND customer_id IS NULL AND guest_name IS NULL) OR (kind = 'Booking' AND (customer_id IS NULL) <> (guest_name IS NULL))");
        }
    }
}
