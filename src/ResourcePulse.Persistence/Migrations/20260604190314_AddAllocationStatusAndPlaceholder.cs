using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    // Phase 4.2: schema changes that back ADR-0015 + ADR-0016.
    //
    // ADR-0015 — Status {Tentative, Hard} dell'allocazione:
    //   - Adds the `status` column (text, max 20), defaulting to 'Tentative' for
    //     the backfill of existing rows. The application default is 'Tentative'
    //     (aggregate factory) — the column default exists only for the backfill.
    //   - I6 (Hard richiede progetto root committato) NON è espressa al DB:
    //     richiede walk cross-aggregate al root via materialized path,
    //     enforced da AllocationService.
    //
    // ADR-0016 — Deallocazione come conversione / placeholder sull'atomo:
    //   - `resource_id` becomes NULLABLE: the Placeholder form has
    //     ResourceId = null.
    //   - Nuove colonne `role_skill_id` (FK Restrict → skills) e
    //     `owner_resource_id` (FK Restrict → resources), valorizzate IFF
    //     `resource_id IS NULL`.
    //   - CHECK constraint ck_allocations_form_xor (I7): esattamente uno tra
    //     { resource_id valorizzato ∧ placeholder fields nulli } e
    //     { resource_id nullo ∧ role_skill_id valorizzato }.
    //   - Indici sui due nuovi FK per supportare i futuri filtri sul
    //     placeholder workflow (ruolo scoperto, owner).
    //
    // Every existing row has resource_id set → they all satisfy the XOR
    // (placeholder fields NULL) e ricevono status = 'Tentative' via backfill.
    public partial class AddAllocationStatusAndPlaceholder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "role_skill_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            // 'Tentative' default for backfilling existing rows (ADR-0015).
            // New rows get their value from the aggregate factory; the
            // column default is inert for application-issued INSERTs.
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "allocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Tentative");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_owner_resource_id",
                table: "allocations",
                column: "owner_resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_role_skill_id",
                table: "allocations",
                column: "role_skill_id");

            // I7 (ADR-0016): forma XOR sull'aggregato.
            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations",
                sql: "(resource_id IS NOT NULL AND role_skill_id IS NULL AND owner_resource_id IS NULL) OR (resource_id IS NULL AND role_skill_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_resources_owner_resource_id",
                table: "allocations",
                column: "owner_resource_id",
                principalTable: "resources",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_skills_role_skill_id",
                table: "allocations",
                column: "role_skill_id",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The rollback fails if placeholder rows exist (resource_id
            // NULL): AlterColumn → NOT NULL violates the constraint. Resolve first
            // i placeholder (riassegnandoli o cancellandoli) e poi rollback.
            migrationBuilder.DropForeignKey(
                name: "fk_allocations_resources_owner_resource_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_skills_role_skill_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_owner_resource_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_role_skill_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "owner_resource_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "role_skill_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "status",
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
        }
    }
}
