using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAllocationToCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 5.1 (ADR-0025) inverts the atom: Allocation becomes a COVERAGE
            // that references a Demand (new required demand_id) and always has a
            // resource (resource_id NOT NULL). The old placeholder columns
            // (role_id, owner_resource_id) and the form-XOR CHECK are dropped.
            // Clean break — the product was never released, so existing allocation
            // rows carry no demand and cannot satisfy the new NOT NULL FK. We
            // truncate rather than backfill (no data preservation, by decision).
            migrationBuilder.Sql("TRUNCATE TABLE allocations;");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_resources_owner_resource_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_roles_role_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_owner_resource_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_role_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "owner_resource_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "allocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "resource_id",
                table: "allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "demand_id",
                table: "allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_allocations_demand_id",
                table: "allocations",
                column: "demand_id");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_demands_demand_id",
                table: "allocations",
                column: "demand_id",
                principalTable: "demands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_allocations_demands_demand_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_demand_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "demand_id",
                table: "allocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "resource_id",
                table: "allocations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_resource_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocations_owner_resource_id",
                table: "allocations",
                column: "owner_resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_role_id",
                table: "allocations",
                column: "role_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations",
                sql: "(resource_id IS NOT NULL AND role_id IS NULL AND owner_resource_id IS NULL) OR (resource_id IS NULL AND role_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_resources_owner_resource_id",
                table: "allocations",
                column: "owner_resource_id",
                principalTable: "resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_roles_role_id",
                table: "allocations",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
