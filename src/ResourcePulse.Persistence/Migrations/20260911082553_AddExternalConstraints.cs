using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalConstraints : Migration
    {
        // Tenant data (ADR-0034 §4): an imposed date belongs to a customer's
        // project. The 29th table under RLS. Explicit, not reflected, like every
        // entry before it — adding a table has to force the question.
        private static readonly string[] TenantScopedTables = ["external_constraints"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_constraints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    root_project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    authority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_constraints", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_constraints_project_nodes_root_project_id",
                        column: x => x.root_project_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocations_end_anchor_constraint_id",
                table: "allocations",
                column: "end_anchor_constraint_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_start_anchor_constraint_id",
                table: "allocations",
                column: "start_anchor_constraint_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_constraints_root_project_id",
                table: "external_constraints",
                column: "root_project_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_constraints_tenant_id",
                table: "external_constraints",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_external_constraints_end_anchor_constraint_id",
                table: "allocations",
                column: "end_anchor_constraint_id",
                principalTable: "external_constraints",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_external_constraints_start_anchor_constraint_id",
                table: "allocations",
                column: "start_anchor_constraint_id",
                principalTable: "external_constraints",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── Row-level security ───────────────────────────────────────────
            // ENABLE + FORCE, USING + WITH CHECK: the same shape as every tenant
            // table (AddTenantIsolationAndRowLevelSecurity). FORCE binds the table
            // owner too; the application connects as the non-superuser role.
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");
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

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_external_constraints_end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_external_constraints_start_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropTable(
                name: "external_constraints");

            migrationBuilder.DropIndex(
                name: "ix_allocations_end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_start_anchor_constraint_id",
                table: "allocations");
        }
    }
}
