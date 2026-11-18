using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "featured_rank",
                table: "venues",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "app_banners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_en = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    body_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    image_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    link_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    link_target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_banners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_content",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    min_android_build = table.Column<int>(type: "integer", nullable: false),
                    min_ios_build = table.Column<int>(type: "integer", nullable: false),
                    android_store_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ios_store_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    maintenance = table.Column<bool>(type: "boolean", nullable: false),
                    maintenance_message_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    maintenance_message_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_content", x => x.id);
                    table.CheckConstraint("ck_app_content_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "broadcasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_en = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    body_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    devices = table.Column<int>(type: "integer", nullable: false),
                    delivered = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_broadcasts", x => x.id);
                    table.ForeignKey(
                        name: "fk_broadcasts_staff_sent_by_staff_id",
                        column: x => x.sent_by_staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_content",
                columns: new[] { "id", "android_store_url", "ios_store_url", "maintenance", "maintenance_message_ar", "maintenance_message_en", "min_android_build", "min_ios_build", "updated_at" },
                values: new object[] { 1, null, null, false, null, null, 1, 1, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "ix_app_banners_sort_order",
                table: "app_banners",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_broadcasts_sent_at",
                table: "broadcasts",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ix_broadcasts_sent_by_staff_id",
                table: "broadcasts",
                column: "sent_by_staff_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_banners");

            migrationBuilder.DropTable(
                name: "app_content");

            migrationBuilder.DropTable(
                name: "broadcasts");

            migrationBuilder.DropColumn(
                name: "featured_rank",
                table: "venues");
        }
    }
}
