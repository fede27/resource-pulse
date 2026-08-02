using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsolationAndRowLevelSecurity : Migration
    {
        // Every table that becomes tenant-scoped. The list is explicit rather
        // than reflected so that adding a table forces a conscious decision about
        // whether it is tenant data (and therefore needs a policy) or not.
        private static readonly string[] TenantScopedTables =
        [
            "allocations",
            "bucketing_defaults",
            "business_calendar_work_windows",
            "business_calendars",
            "commitment_policies",
            "company_closures",
            "demands",
            "load_band_configurations",
            "load_bands",
            "project_node_tags",
            "project_nodes",
            "project_skill_requirements",
            "resource_adjustments",
            "resource_skills",
            "resource_tags",
            "resource_work_windows",
            "resources",
            "roles",
            "skills",
            "tags",
            "teams",
            "time_fence_configurations"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Clean break ──────────────────────────────────────────────────
            // There is no correct backfill value for tenant_id on pre-existing
            // rows: any constant we invent would be a fabricated ownership claim,
            // and rows left at the empty UUID are invisible to every tenant and
            // rejected by the policies below. Development data is discarded and
            // re-seeded under an explicit tenant. Same posture as
            // RefactorAllocationToCoverage: NEVER run this against production.
            migrationBuilder.Sql(
                $"TRUNCATE TABLE {string.Join(", ", TenantScopedTables)} CASCADE;");

            migrationBuilder.DropIndex(
                name: "ux_teams_name",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ux_tags_name",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ux_skills_name",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "ux_roles_name",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ux_resources_email",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ux_resources_user_sub",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ux_project_nodes_parent_code",
                table: "project_nodes");

            migrationBuilder.DropIndex(
                name: "ux_project_nodes_root_code",
                table: "project_nodes");

            migrationBuilder.DropIndex(
                name: "ix_business_calendars_is_default_unique",
                table: "business_calendars");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "time_fence_configurations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "teams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "tags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "skills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "roles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "resource_work_windows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "resource_tags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "resource_skills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "resource_adjustments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_skill_requirements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_nodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "project_node_tags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "load_bands",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "load_band_configurations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "demands",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "company_closures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "commitment_policies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "business_calendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "business_calendar_work_windows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "bucketing_defaults",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ux_time_fence_configurations_tenant",
                table: "time_fence_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teams_tenant_id",
                table: "teams",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_teams_name",
                table: "teams",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_tenant_id",
                table: "tags",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_tags_name",
                table: "tags",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_tenant_id",
                table: "skills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_skills_name",
                table: "skills",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id",
                table: "roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_roles_name",
                table: "roles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resources_tenant_id",
                table: "resources",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_resources_email",
                table: "resources",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resources_user_sub",
                table: "resources",
                columns: new[] { "tenant_id", "user_sub" },
                unique: true,
                filter: "user_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_resource_work_windows_tenant_id",
                table: "resource_work_windows",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_tags_tenant_id",
                table: "resource_tags",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_skills_tenant_id",
                table: "resource_skills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_adjustments_tenant_id",
                table: "resource_adjustments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_skill_requirements_tenant_id",
                table: "project_skill_requirements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_nodes_tenant_id",
                table: "project_nodes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_project_nodes_parent_code",
                table: "project_nodes",
                columns: new[] { "tenant_id", "parent_id", "code" },
                unique: true,
                filter: "parent_id IS NOT NULL AND code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_project_nodes_root_code",
                table: "project_nodes",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "parent_id IS NULL AND code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_node_tags_tenant_id",
                table: "project_node_tags",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_load_bands_tenant_id",
                table: "load_bands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_load_band_configurations_tenant",
                table: "load_band_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_demands_tenant_id",
                table: "demands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_closures_tenant_id",
                table: "company_closures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_commitment_policies_tenant",
                table: "commitment_policies",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_business_calendars_is_default_unique",
                table: "business_calendars",
                columns: new[] { "tenant_id", "is_default" },
                unique: true,
                filter: "is_default = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_business_calendars_tenant_id",
                table: "business_calendars",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_calendar_work_windows_tenant_id",
                table: "business_calendar_work_windows",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_bucketing_defaults_tenant",
                table: "bucketing_defaults",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocations_tenant_id",
                table: "allocations",
                column: "tenant_id");

            // ── Row-level security ───────────────────────────────────────────
            foreach (var table in TenantScopedTables)
            {
                // The column default exists only so the ADD COLUMN above can be
                // NOT NULL. Leaving it in place would let an insert that forgot
                // the tenant silently land on the empty UUID; dropping it makes
                // that a NOT NULL violation instead.
                migrationBuilder.Sql($"ALTER TABLE {table} ALTER COLUMN tenant_id DROP DEFAULT;");

                migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");

                // FORCE makes the policy apply to the table owner too. Without it
                // the owner — which is who runs migrations and seeding — would
                // silently bypass isolation. Note that a Postgres SUPERUSER
                // bypasses RLS regardless: that is precisely why the application
                // connects as a dedicated non-superuser role (see the container
                // init script), and why running the API as `postgres` would make
                // these policies inert.
                migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");

                // USING governs what is visible; WITH CHECK governs what may be
                // written. Both are needed: USING alone would let a request write
                // a row into another tenant that it then could not see.
                migrationBuilder.Sql($"""
                    CREATE POLICY {table}_tenant_isolation ON {table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {table}_tenant_isolation ON {table};");
                migrationBuilder.Sql($"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropIndex(
                name: "ux_time_fence_configurations_tenant",
                table: "time_fence_configurations");

            migrationBuilder.DropIndex(
                name: "ix_teams_tenant_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ux_teams_name",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_tags_tenant_id",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ux_tags_name",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_skills_tenant_id",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "ux_skills_name",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ux_roles_name",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_resources_tenant_id",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ux_resources_email",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ux_resources_user_sub",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ix_resource_work_windows_tenant_id",
                table: "resource_work_windows");

            migrationBuilder.DropIndex(
                name: "ix_resource_tags_tenant_id",
                table: "resource_tags");

            migrationBuilder.DropIndex(
                name: "ix_resource_skills_tenant_id",
                table: "resource_skills");

            migrationBuilder.DropIndex(
                name: "ix_resource_adjustments_tenant_id",
                table: "resource_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_project_skill_requirements_tenant_id",
                table: "project_skill_requirements");

            migrationBuilder.DropIndex(
                name: "ix_project_nodes_tenant_id",
                table: "project_nodes");

            migrationBuilder.DropIndex(
                name: "ux_project_nodes_parent_code",
                table: "project_nodes");

            migrationBuilder.DropIndex(
                name: "ux_project_nodes_root_code",
                table: "project_nodes");

            migrationBuilder.DropIndex(
                name: "ix_project_node_tags_tenant_id",
                table: "project_node_tags");

            migrationBuilder.DropIndex(
                name: "ix_load_bands_tenant_id",
                table: "load_bands");

            migrationBuilder.DropIndex(
                name: "ux_load_band_configurations_tenant",
                table: "load_band_configurations");

            migrationBuilder.DropIndex(
                name: "ix_demands_tenant_id",
                table: "demands");

            migrationBuilder.DropIndex(
                name: "ix_company_closures_tenant_id",
                table: "company_closures");

            migrationBuilder.DropIndex(
                name: "ux_commitment_policies_tenant",
                table: "commitment_policies");

            migrationBuilder.DropIndex(
                name: "ix_business_calendars_is_default_unique",
                table: "business_calendars");

            migrationBuilder.DropIndex(
                name: "ix_business_calendars_tenant_id",
                table: "business_calendars");

            migrationBuilder.DropIndex(
                name: "ix_business_calendar_work_windows_tenant_id",
                table: "business_calendar_work_windows");

            migrationBuilder.DropIndex(
                name: "ux_bucketing_defaults_tenant",
                table: "bucketing_defaults");

            migrationBuilder.DropIndex(
                name: "ix_allocations_tenant_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "time_fence_configurations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "resource_work_windows");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "resource_tags");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "resource_skills");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "resource_adjustments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_skill_requirements");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_nodes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "project_node_tags");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "load_bands");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "load_band_configurations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "demands");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "company_closures");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "commitment_policies");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "business_calendars");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "business_calendar_work_windows");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "bucketing_defaults");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "allocations");

            migrationBuilder.CreateIndex(
                name: "ux_teams_name",
                table: "teams",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_skills_name",
                table: "skills",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_resources_email",
                table: "resources",
                column: "email",
                unique: true,
                filter: "email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_resources_user_sub",
                table: "resources",
                column: "user_sub",
                unique: true,
                filter: "user_sub IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_project_nodes_parent_code",
                table: "project_nodes",
                columns: new[] { "parent_id", "code" },
                unique: true,
                filter: "parent_id IS NOT NULL AND code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_project_nodes_root_code",
                table: "project_nodes",
                column: "code",
                unique: true,
                filter: "parent_id IS NULL AND code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_business_calendars_is_default_unique",
                table: "business_calendars",
                column: "is_default",
                unique: true,
                filter: "is_default = TRUE");
        }
    }
}
