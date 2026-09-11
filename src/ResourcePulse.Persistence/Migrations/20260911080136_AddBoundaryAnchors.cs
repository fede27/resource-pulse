using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoundaryAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "end_anchor_constraint_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "end_anchor_kind",
                table: "allocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pinned");

            migrationBuilder.AddColumn<Guid>(
                name: "end_anchor_node_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "start_anchor_constraint_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "start_anchor_kind",
                table: "allocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pinned");

            migrationBuilder.AddColumn<Guid>(
                name: "start_anchor_node_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocations_end_anchor_node_id",
                table: "allocations",
                column: "end_anchor_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_start_anchor_node_id",
                table: "allocations",
                column: "start_anchor_node_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_end_anchor_shape",
                table: "allocations",
                sql: "(end_anchor_kind IN ('NodeStart', 'NodeEnd') AND end_anchor_node_id IS NOT NULL AND end_anchor_constraint_id IS NULL) OR (end_anchor_kind = 'External' AND end_anchor_constraint_id IS NOT NULL AND end_anchor_node_id IS NULL) OR (end_anchor_kind IN ('Pinned', 'ResourceAvailability') AND end_anchor_node_id IS NULL AND end_anchor_constraint_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_start_anchor_shape",
                table: "allocations",
                sql: "(start_anchor_kind IN ('NodeStart', 'NodeEnd') AND start_anchor_node_id IS NOT NULL AND start_anchor_constraint_id IS NULL) OR (start_anchor_kind = 'External' AND start_anchor_constraint_id IS NOT NULL AND start_anchor_node_id IS NULL) OR (start_anchor_kind IN ('Pinned', 'ResourceAvailability') AND start_anchor_node_id IS NULL AND start_anchor_constraint_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_project_nodes_end_anchor_node_id",
                table: "allocations",
                column: "end_anchor_node_id",
                principalTable: "project_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_project_nodes_start_anchor_node_id",
                table: "allocations",
                column: "start_anchor_node_id",
                principalTable: "project_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_allocations_project_nodes_end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_project_nodes_start_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_start_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_end_anchor_shape",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_start_anchor_shape",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "end_anchor_kind",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "start_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "start_anchor_kind",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "start_anchor_node_id",
                table: "allocations");
        }
    }
}
