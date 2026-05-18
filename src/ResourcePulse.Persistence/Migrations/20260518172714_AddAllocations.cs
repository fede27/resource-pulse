using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ProjectNode planning columns ────────────────────────────────
            migrationBuilder.AddColumn<TimeSpan>(
                name: "estimated_work",
                table: "project_nodes",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "planning_mode",
                table: "project_nodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Backfill: every existing Project/Phase row gets PlanningMode = 'Unspecified'.
            // WorkPackage rows stay NULL (the capacity-planning rule excludes them).
            migrationBuilder.Sql(@"
                UPDATE project_nodes
                SET planning_mode = 'Unspecified'
                WHERE node_type IN ('Project','Phase') AND planning_mode IS NULL;
            ");

            // ── Allocations table ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    allocation_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_allocations_project_nodes_project_node_id",
                        column: x => x.project_node_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocations_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocations_project_node_id_period",
                table: "allocations",
                columns: new[] { "project_node_id", "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_allocations_resource_id_period",
                table: "allocations",
                columns: new[] { "resource_id", "period_start", "period_end" });

            // ── CHECK constraints on project_nodes ──────────────────────────
            // PlanningMode is non-null iff the node can hold capacity-planning artifacts.
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_planning_mode_level
                CHECK (
                    (node_type IN ('Project','Phase') AND planning_mode IS NOT NULL)
                    OR (node_type NOT IN ('Project','Phase') AND planning_mode IS NULL)
                );
            ");

            // EstimatedWork is non-null AND positive iff PlanningMode = 'FixedWork'.
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_estimated_work_mode
                CHECK (
                    (planning_mode = 'FixedWork' AND estimated_work IS NOT NULL AND estimated_work > interval '0')
                    OR ((planning_mode IS NULL OR planning_mode <> 'FixedWork') AND estimated_work IS NULL)
                );
            ");

            // ── No-overlap EXCLUDE constraint on allocations ────────────────
            // btree_gist is required so the gist index can use the '=' operator on
            // uuid columns alongside daterange '&&'. The '[]' bound on daterange
            // matches the domain's inclusive PeriodEnd; adjacent allocations
            // (one ends day N, next starts day N+1) do not overlap.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(@"
                ALTER TABLE allocations
                ADD CONSTRAINT allocations_no_overlap
                EXCLUDE USING gist (
                    resource_id WITH =,
                    project_node_id WITH =,
                    daterange(period_start, period_end, '[]') WITH &&
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EXCLUDE constraint and table.
            migrationBuilder.Sql("ALTER TABLE allocations DROP CONSTRAINT IF EXISTS allocations_no_overlap;");
            migrationBuilder.DropTable(name: "allocations");

            // CHECK constraints (drop before dropping the columns they reference).
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_estimated_work_mode;");
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_planning_mode_level;");

            migrationBuilder.DropColumn(name: "estimated_work", table: "project_nodes");
            migrationBuilder.DropColumn(name: "planning_mode", table: "project_nodes");

            // Leave btree_gist installed — dropping an extension can fail if any
            // object still depends on it, and other future features may also need it.
        }
    }
}
