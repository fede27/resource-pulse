using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapacityModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_calendars", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_closures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_closures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "business_calendar_work_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    business_calendar_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_calendar_work_windows", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_calendar_work_windows_business_calendars_business_",
                        column: x => x.business_calendar_id,
                        principalTable: "business_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    business_calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_resources_business_calendars_business_calendar_id",
                        column: x => x.business_calendar_id,
                        principalTable: "business_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hours = table.Column<TimeSpan>(type: "interval", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_adjustments_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_work_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_work_windows", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_work_windows_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_calendar_work_windows_business_calendar_id_valid_f",
                table: "business_calendar_work_windows",
                columns: new[] { "business_calendar_id", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_business_calendars_is_default_unique",
                table: "business_calendars",
                column: "is_default",
                unique: true,
                filter: "is_default = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_company_closures_date_from_date_to",
                table: "company_closures",
                columns: new[] { "date_from", "date_to" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_adjustments_resource_id_date_from_date_to",
                table: "resource_adjustments",
                columns: new[] { "resource_id", "date_from", "date_to" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_work_windows_resource_id_valid_from_valid_to",
                table: "resource_work_windows",
                columns: new[] { "resource_id", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_business_calendar_id",
                table: "resources",
                column: "business_calendar_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_calendar_work_windows");

            migrationBuilder.DropTable(
                name: "company_closures");

            migrationBuilder.DropTable(
                name: "resource_adjustments");

            migrationBuilder.DropTable(
                name: "resource_work_windows");

            migrationBuilder.DropTable(
                name: "resources");

            migrationBuilder.DropTable(
                name: "business_calendars");
        }
    }
}
