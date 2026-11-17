using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Bookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AddColumn<int>(
                name: "free_cancellation_hours",
                table: "venues",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    net = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    free_cancellation_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    late_cancellation = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_ends_after_start", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_bookings_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_court_id_starts_at",
                table: "bookings",
                columns: new[] { "court_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_customer_id_starts_at",
                table: "bookings",
                columns: new[] { "customer_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_reference",
                table: "bookings",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_venue_id",
                table: "bookings",
                column: "venue_id");

            // The last line of defence against double bookings: two confirmed bookings of one court
            // may never overlap, whatever the application does. Ranges are half-open, so 18:00–19:30
            // and 19:30–21:00 can both exist.
            migrationBuilder.Sql("""
                ALTER TABLE bookings
                ADD CONSTRAINT ex_bookings_no_overlap
                EXCLUDE USING gist (court_id WITH =, tstzrange(starts_at, ends_at, '[)') WITH &&)
                WHERE (status = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE bookings DROP CONSTRAINT IF EXISTS ex_bookings_no_overlap;");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropColumn(
                name: "free_cancellation_hours",
                table: "venues");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
