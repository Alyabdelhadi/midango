using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fee_mode",
                table: "venues",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Default");

            migrationBuilder.AddColumn<decimal>(
                name: "fee_monthly",
                table: "venues",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fee_percent",
                table: "venues",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operator_role",
                table: "staff",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "commission_percent",
                table: "platform_settings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AlterColumn<Guid>(
                name: "venue_id",
                table: "operator_actions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "error_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    exception_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    detail = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "venue_statements",
                columns: table => new
                {
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_statements", x => new { x.venue_id, x.month });
                    table.ForeignKey(
                        name: "fk_venue_statements_staff_paid_by_staff_id",
                        column: x => x.paid_by_staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_venue_statements_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "platform_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "commission_percent",
                value: 10m);

            migrationBuilder.CreateIndex(
                name: "ix_operator_actions_at",
                table: "operator_actions",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_error_entries_at",
                table: "error_entries",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_venue_statements_paid_by_staff_id",
                table: "venue_statements",
                column: "paid_by_staff_id");

            // Every operator before roles existed could do everything: they start as Owners.
            migrationBuilder.Sql("UPDATE staff SET operator_role = 'Owner' WHERE role = 'PlatformOperator';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "error_entries");

            migrationBuilder.DropTable(
                name: "venue_statements");

            migrationBuilder.DropIndex(
                name: "ix_operator_actions_at",
                table: "operator_actions");

            migrationBuilder.DropColumn(
                name: "fee_mode",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "fee_monthly",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "fee_percent",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "operator_role",
                table: "staff");

            migrationBuilder.DropColumn(
                name: "commission_percent",
                table: "platform_settings");

            migrationBuilder.AlterColumn<Guid>(
                name: "venue_id",
                table: "operator_actions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
