using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Catalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_ar",
                table: "venues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_en",
                table: "venues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "venues",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "venues",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "published",
                table: "venues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "venues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Riyadh");

            migrationBuilder.CreateTable(
                name: "opening_hours",
                columns: table => new
                {
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    opens = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_hours", x => new { x.venue_id, x.day });
                    table.CheckConstraint("ck_opening_hours_not_empty", "opens <> closes");
                    table.ForeignKey(
                        name: "fk_opening_hours_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sport_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    icon_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    durations_minutes = table.Column<int[]>(type: "integer[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sport_categories", x => x.id);
                    table.CheckConstraint("ck_sport_categories_durations", "cardinality(durations_minutes) BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "court_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_court_features", x => x.id);
                    table.ForeignKey(
                        name: "fk_court_features_sport_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "sport_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    indoor = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courts", x => x.id);
                    table.ForeignKey(
                        name: "fk_courts_sport_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "sport_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_courts_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "court_feature_links",
                columns: table => new
                {
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    features_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_court_feature_links", x => new { x.court_id, x.features_id });
                    table.ForeignKey(
                        name: "fk_court_feature_links_court_features_features_id",
                        column: x => x.features_id,
                        principalTable: "court_features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_court_feature_links_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_court_feature_links_features_id",
                table: "court_feature_links",
                column: "features_id");

            migrationBuilder.CreateIndex(
                name: "ix_court_features_category_id",
                table: "court_features",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_courts_category_id",
                table: "courts",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_courts_venue_id_sort_order",
                table: "courts",
                columns: new[] { "venue_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "court_feature_links");

            migrationBuilder.DropTable(
                name: "opening_hours");

            migrationBuilder.DropTable(
                name: "court_features");

            migrationBuilder.DropTable(
                name: "courts");

            migrationBuilder.DropTable(
                name: "sport_categories");

            migrationBuilder.DropColumn(
                name: "address_ar",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "address_en",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "published",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "venues");
        }
    }
}
