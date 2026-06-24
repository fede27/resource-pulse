using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    // ADR-0020: four org-level configuration singletons — boundaries & thresholds,
    // not rules. Each is its own aggregate with its own table and an opinionated
    // default row seeded below (the services also get-or-seed defensively).
    /// <inheritdoc />
    public partial class AddOrgConfiguration : Migration
    {
        // Well-known singleton ids — must match the SingletonId constants on the
        // domain aggregates.
        private static readonly Guid LoadBandConfigId = new("a1b1c1d1-0000-0000-0000-000000000001");
        private static readonly Guid TimeFenceConfigId = new("a1b1c1d1-0000-0000-0000-000000000002");
        private static readonly Guid BucketingDefaultsId = new("a1b1c1d1-0000-0000-0000-000000000003");
        private static readonly Guid CommitmentPolicyId = new("a1b1c1d1-0000-0000-0000-000000000004");
        private static readonly DateTime SeededAt = new(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
        private const string SeededBy = "migration";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bucketing_defaults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_grain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    secondary_grain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bucketing_defaults", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commitment_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    hard_commit_levels = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commitment_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "load_band_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_load_band_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "time_fence_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    frozen_horizon_value = table.Column<int>(type: "integer", nullable: false),
                    frozen_horizon_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slushy_horizon_value = table.Column<int>(type: "integer", nullable: false),
                    slushy_horizon_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_fence_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "load_bands",
                columns: table => new
                {
                    lower_bound = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    load_band_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_load_bands", x => new { x.load_band_configuration_id, x.lower_bound });
                    table.ForeignKey(
                        name: "fk_load_bands_load_band_configurations_load_band_configuration",
                        column: x => x.load_band_configuration_id,
                        principalTable: "load_band_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Seed opinionated defaults (ADR-0020) ────────────────────────────
            migrationBuilder.InsertData(
                table: "bucketing_defaults",
                columns: new[] { "id", "created_at", "created_by", "primary_grain", "secondary_grain" },
                values: new object[] { BucketingDefaultsId, SeededAt, SeededBy, "Week", "Month" });

            // Default hard-commit threshold {Committed, Critical} — the value
            // previously cabled in PlanCommandService / ProjectNodeService.
            migrationBuilder.InsertData(
                table: "commitment_policies",
                columns: new[] { "id", "created_at", "created_by", "hard_commit_levels" },
                values: new object[] { CommitmentPolicyId, SeededAt, SeededBy, "Committed,Critical" });

            migrationBuilder.InsertData(
                table: "time_fence_configurations",
                columns: new[]
                {
                    "id", "created_at", "created_by",
                    "frozen_horizon_value", "frozen_horizon_unit",
                    "slushy_horizon_value", "slushy_horizon_unit"
                },
                values: new object[] { TimeFenceConfigId, SeededAt, SeededBy, 2, "Weeks", 2, "Months" });

            migrationBuilder.InsertData(
                table: "load_band_configurations",
                columns: new[] { "id", "created_at", "created_by" },
                values: new object[] { LoadBandConfigId, SeededAt, SeededBy });

            // Under(0) · Healthy(85) · Full(100) · Overloaded(110).
            migrationBuilder.InsertData(
                table: "load_bands",
                columns: new[] { "load_band_configuration_id", "lower_bound", "label" },
                values: new object[,]
                {
                    { LoadBandConfigId, 0m, "Under" },
                    { LoadBandConfigId, 85m, "Healthy" },
                    { LoadBandConfigId, 100m, "Full" },
                    { LoadBandConfigId, 110m, "Overloaded" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bucketing_defaults");

            migrationBuilder.DropTable(
                name: "commitment_policies");

            migrationBuilder.DropTable(
                name: "load_bands");

            migrationBuilder.DropTable(
                name: "time_fence_configurations");

            migrationBuilder.DropTable(
                name: "load_band_configurations");
        }
    }
}
