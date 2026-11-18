using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_documents",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    body_en = table.Column<string>(type: "character varying(60000)", maxLength: 60000, nullable: false),
                    body_ar = table.Column<string>(type: "character varying(60000)", maxLength: 60000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_documents", x => x.kind);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_documents");
        }
    }
}
