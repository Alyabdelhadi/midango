using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Prices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "venues",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percent",
                table: "venues",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 15m);

            migrationBuilder.AddColumn<decimal>(
                name: "hourly_price",
                table: "courts",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateTable(
                name: "price_bands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    from = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    to = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hourly_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_bands", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_bands_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_bands_court_id_day",
                table: "price_bands",
                columns: new[] { "court_id", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_bands");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "vat_percent",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "hourly_price",
                table: "courts");
        }
    }
}
