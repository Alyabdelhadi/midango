using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VenueControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "suspended",
                table: "venues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "operator_actions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_operator_actions_staff_operator_id",
                        column: x => x.operator_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operator_actions_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operator_actions_operator_id",
                table: "operator_actions",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_operator_actions_venue_id_at",
                table: "operator_actions",
                columns: new[] { "venue_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_actions");

            migrationBuilder.DropColumn(
                name: "suspended",
                table: "venues");
        }
    }
}
