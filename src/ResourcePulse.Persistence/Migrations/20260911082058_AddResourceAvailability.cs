using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "available_from",
                table: "resources",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "available_until",
                table: "resources",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_resources_availability_ordered",
                table: "resources",
                sql: "available_from IS NULL OR available_until IS NULL OR available_from <= available_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_resources_availability_ordered",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "available_from",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "available_until",
                table: "resources");
        }
    }
}
