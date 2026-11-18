using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "blocked_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "blocked_by_staff_id",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blocked_reason",
                table: "customers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "no_shows_cleared_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_blocked_by_staff_id",
                table: "customers",
                column: "blocked_by_staff_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_staff_blocked_by_staff_id",
                table: "customers",
                column: "blocked_by_staff_id",
                principalTable: "staff",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_staff_blocked_by_staff_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ix_customers_blocked_by_staff_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "blocked_by_staff_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "blocked_reason",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "no_shows_cleared_at",
                table: "customers");
        }
    }
}
