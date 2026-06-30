using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientAndRetargetPlaceholderRoleToRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M2 (ADR-0021): the placeholder "open role" is re-targeted from the
            // Skill catalogue to the Role catalogue (the same catalogue as
            // Resource.RoleId). The column is renamed role_skill_id -> role_id and
            // its FK now points at `roles`. NOTE: any pre-existing placeholder rows
            // whose role_skill_id referenced a `skills` row must be remapped to a
            // `roles` id before this migration's new FK is enforced; placeholders
            // are a Phase 4.2 feature with no production data at this point, so no
            // data backfill is included here.
            migrationBuilder.DropForeignKey(
                name: "fk_allocations_skills_role_skill_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations");

            migrationBuilder.RenameColumn(
                name: "role_skill_id",
                table: "allocations",
                newName: "role_id");

            migrationBuilder.RenameIndex(
                name: "ix_allocations_role_skill_id",
                table: "allocations",
                newName: "ix_allocations_role_id");

            migrationBuilder.AddColumn<string>(
                name: "client",
                table: "project_nodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // M1: `client` is a project-only field — populated only on root
            // (Project) rows, NULL on Phase/WorkPackage. Mirrors the
            // ck_project_nodes_project_only_fields pattern (Phase 3 migration).
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_client_root_only
                CHECK (node_type = 'Project' OR client IS NULL);
            ");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations",
                sql: "(resource_id IS NOT NULL AND role_id IS NULL AND owner_resource_id IS NULL) OR (resource_id IS NULL AND role_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_roles_role_id",
                table: "allocations",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_allocations_roles_role_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations");

            migrationBuilder.Sql(
                "ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_client_root_only;");

            migrationBuilder.DropColumn(
                name: "client",
                table: "project_nodes");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "allocations",
                newName: "role_skill_id");

            migrationBuilder.RenameIndex(
                name: "ix_allocations_role_id",
                table: "allocations",
                newName: "ix_allocations_role_skill_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_form_xor",
                table: "allocations",
                sql: "(resource_id IS NOT NULL AND role_skill_id IS NULL AND owner_resource_id IS NULL) OR (resource_id IS NULL AND role_skill_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_skills_role_skill_id",
                table: "allocations",
                column: "role_skill_id",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
