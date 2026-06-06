using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    // Phase 4.2: schema changes that back ADR-0015 + ADR-0016.
    //
    // ADR-0015 — Status {Tentative, Hard} dell'allocazione:
    //   - Aggiunge la colonna `status` (text, max 20), default 'Tentative' al
    //     backfill delle righe esistenti. Default applicativo è 'Tentative'
    //     (aggregate factory) — il default di colonna è solo per il backfill.
    //   - I6 (Hard richiede progetto root committato) NON è espressa al DB:
    //     richiede walk cross-aggregate al root via materialized path,
    //     enforced da AllocationService.
    //
    // ADR-0016 — Deallocazione come conversione / placeholder sull'atomo:
    //   - `resource_id` diventa NULLABLE: lo stato Placeholder ha
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
    // Le righe esistenti hanno tutte resource_id valorizzato → passano l'XOR
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

            // Default 'Tentative' al backfill delle righe esistenti (ADR-0015).
            // Le nuove righe ricevono il valore dall'aggregate factory; il
            // default di colonna è inerte in fase di INSERT applicativo.
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
            // Il rollback fallisce se esistono righe placeholder (resource_id
            // NULL): AlterColumn → NOT NULL viola il vincolo. Risolvere prima
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
