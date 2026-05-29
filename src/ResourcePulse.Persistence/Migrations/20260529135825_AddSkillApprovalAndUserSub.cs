using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillApprovalAndUserSub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_sub",
                table: "resources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Backfills any pre-existing rows to the Pending state. The
            // ResourceSkill aggregate sets ApprovalStatus on Create, so the
            // application never inserts a row without a value — but the
            // default is kept at the SQL level as a safety net.
            migrationBuilder.AddColumn<string>(
                name: "approval_status",
                table: "resource_skills",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "resource_skills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_resource_id",
                table: "resource_skills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_resources_user_sub",
                table: "resources",
                column: "user_sub",
                unique: true,
                filter: "user_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_resource_skills_reviewed_by_resource_id",
                table: "resource_skills",
                column: "reviewed_by_resource_id");

            migrationBuilder.AddForeignKey(
                name: "fk_resource_skills_resources_reviewed_by_resource_id",
                table: "resource_skills",
                column: "reviewed_by_resource_id",
                principalTable: "resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_resource_skills_resources_reviewed_by_resource_id",
                table: "resource_skills");

            migrationBuilder.DropIndex(
                name: "ux_resources_user_sub",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ix_resource_skills_reviewed_by_resource_id",
                table: "resource_skills");

            migrationBuilder.DropColumn(
                name: "user_sub",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "resource_skills");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "resource_skills");

            migrationBuilder.DropColumn(
                name: "reviewed_by_resource_id",
                table: "resource_skills");
        }
    }
}
