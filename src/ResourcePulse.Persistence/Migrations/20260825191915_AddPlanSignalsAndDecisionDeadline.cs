using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSignalsAndDecisionDeadline : Migration
    {
        // Tables 24–28 of the tenant-scoped set (ADR-0029 keeps this list explicit
        // rather than reflected, so that adding a table forces the question "is
        // this tenant data?"). All five are: the triage queue and its
        // acknowledgements are the tenant's, the sweep state and the policy are
        // per tenant, and signal_visits is per-user BUT still tenant-scoped —
        // the same person can belong to several tenants and their reading of one
        // says nothing about the others.
        private static readonly string[] TenantScopedTables =
        [
            "plan_signals",
            "signal_acknowledgements",
            "signal_policies",
            "signal_sweep_states",
            "signal_visits"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "decide_by",
                table: "demands",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "plan_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detection = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deadline_at = table.Column<DateOnly>(type: "date", nullable: true),
                    zone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    magnitude = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    hard_committed = table.Column<bool>(type: "boolean", nullable: false),
                    first_detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_change = table.Column<int>(type: "integer", nullable: false),
                    previous_magnitude = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    previous_zone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_subject_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    touched_root_project_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_signals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signal_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_retention_days = table.Column<int>(type: "integer", nullable: false),
                    decision_lead_time_value = table.Column<int>(type: "integer", nullable: false),
                    decision_lead_time_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_policies", x => x.id);
                    table.CheckConstraint("ck_signal_policies_retention_positive", "resolved_retention_days >= 1");
                });

            migrationBuilder.CreateTable(
                name: "signal_sweep_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_swept_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_live_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_sweep_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signal_visits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_sub = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    last_visited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_visits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signal_acknowledgements",
                columns: table => new
                {
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    plan_signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_acknowledgements", x => new { x.plan_signal_id, x.sequence });
                    table.ForeignKey(
                        name: "fk_signal_acknowledgements_plan_signals_plan_signal_id",
                        column: x => x.plan_signal_id,
                        principalTable: "plan_signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_signals_detection",
                table: "plan_signals",
                columns: new[] { "tenant_id", "detection" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_signals_last_changed_at",
                table: "plan_signals",
                columns: new[] { "tenant_id", "last_changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_signals_tenant_id",
                table: "plan_signals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_signals_touched_roots",
                table: "plan_signals",
                column: "touched_root_project_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_signal_acknowledgements_tenant_id",
                table: "signal_acknowledgements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_signal_policies_tenant",
                table: "signal_policies",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_signal_sweep_states_tenant",
                table: "signal_sweep_states",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signal_visits_tenant_id",
                table: "signal_visits",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_signal_visits_user_sub",
                table: "signal_visits",
                columns: new[] { "tenant_id", "user_sub" },
                unique: true);

            // ── Signal identity (ADR-0032 §2) ────────────────────────────────
            // (tenant, kind, subject) is unique AMONG LIVE ROWS only. Resolved
            // rows are historical records, and a condition that returns gets a NEW
            // row — which is what keeps first_detected_at unambiguous (the unseen
            // dot) and makes "an acknowledgement does not survive resolution"
            // structural instead of a rule somebody has to enforce.
            //
            // NULLS NOT DISTINCT is load-bearing, not a flourish: aggregate kinds
            // carry a NULL subject_id, and under the default NULLS DISTINCT two
            // live aggregate rows of the same kind would not collide at all — the
            // detector's reconciliation would duplicate them in silence. Raw SQL
            // because EF cannot express it.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_plan_signals_live_identity
                    ON plan_signals (tenant_id, kind, subject_id)
                    NULLS NOT DISTINCT
                    WHERE detection = 'Live';
                """);

            // ── Row-level security ───────────────────────────────────────────
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");

                // FORCE so the policy binds the table owner too — who is who runs
                // migrations and seeding. A Postgres SUPERUSER bypasses RLS
                // regardless, which is why the application (and the signal worker)
                // connect as the dedicated non-superuser role.
                migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");

                // USING governs visibility, WITH CHECK governs writes. The worker
                // writes signals under TenantScope.For(id), so WITH CHECK is what
                // stops a detection pass from ever landing a row in the wrong
                // tenant.
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

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_plan_signals_live_identity;");

            migrationBuilder.DropTable(
                name: "signal_acknowledgements");

            migrationBuilder.DropTable(
                name: "signal_policies");

            migrationBuilder.DropTable(
                name: "signal_sweep_states");

            migrationBuilder.DropTable(
                name: "signal_visits");

            migrationBuilder.DropTable(
                name: "plan_signals");

            migrationBuilder.DropColumn(
                name: "decide_by",
                table: "demands");
        }
    }
}
