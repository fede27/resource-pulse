using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsSkillsTagsProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "resources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    node_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    path = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    commitment_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    lead_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    baseline_start = table.Column<DateOnly>(type: "date", nullable: true),
                    baseline_end = table.Column<DateOnly>(type: "date", nullable: true),
                    baselined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planned_start = table.Column<DateOnly>(type: "date", nullable: true),
                    planned_end = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_start = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_end = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_nodes_project_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_nodes_resources_lead_resource_id",
                        column: x => x.lead_resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "citext", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "citext", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_skill_requirements",
                columns: table => new
                {
                    project_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_skill_requirements", x => new { x.project_node_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_project_skill_requirements_project_nodes_project_node_id",
                        column: x => x.project_node_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_skill_requirements_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_skills",
                columns: table => new
                {
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_skills", x => new { x.resource_id, x.skill_id });
                    table.ForeignKey(
                        name: "fk_resource_skills_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resource_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_node_tags",
                columns: table => new
                {
                    project_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_node_tags", x => new { x.project_node_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_project_node_tags_project_nodes_project_node_id",
                        column: x => x.project_node_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_project_node_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resource_tags",
                columns: table => new
                {
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_tags", x => new { x.resource_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_resource_tags_resources_resource_id",
                        column: x => x.resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resource_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resources_team_id",
                table: "resources",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_node_tags_tag_id",
                table: "project_node_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_nodes_lead_resource_id",
                table: "project_nodes",
                column: "lead_resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_nodes_node_type_status",
                table: "project_nodes",
                columns: new[] { "node_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_project_nodes_parent_id",
                table: "project_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_nodes_path",
                table: "project_nodes",
                column: "path")
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

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
                name: "ix_project_skill_requirements_skill_id",
                table: "project_skill_requirements",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_skills_skill_id",
                table: "resource_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_tags_tag_id",
                table: "resource_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ux_skills_name",
                table: "skills",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_teams_name",
                table: "teams",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_resources_teams_team_id",
                table: "resources",
                column: "team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Date invariants on project_nodes — one CHECK per date set, all NULL-tolerant.
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_baseline_dates
                CHECK (baseline_start IS NULL OR baseline_end IS NULL OR baseline_start <= baseline_end);
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_planned_dates
                CHECK (planned_start IS NULL OR planned_end IS NULL OR planned_start <= planned_end);
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_actual_dates
                CHECK (actual_start IS NULL OR actual_end IS NULL OR actual_start <= actual_end);
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_actual_end_requires_start
                CHECK (actual_end IS NULL OR actual_start IS NOT NULL);
            ");

            // Project-only fields: Type/CommitmentLevel/LeadResourceId/Status are populated
            // only on root (Project) nodes; Phase/WorkPackage rows must keep them NULL.
            // Status is required on root nodes.
            migrationBuilder.Sql(@"
                ALTER TABLE project_nodes
                ADD CONSTRAINT ck_project_nodes_project_only_fields
                CHECK (
                    (node_type = 'Project' AND status IS NOT NULL)
                    OR
                    (node_type <> 'Project'
                        AND type IS NULL
                        AND commitment_level IS NULL
                        AND lead_resource_id IS NULL
                        AND status IS NULL)
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop CHECK constraints first; they reference the project_nodes table dropped below.
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_project_only_fields;");
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_actual_end_requires_start;");
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_actual_dates;");
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_planned_dates;");
            migrationBuilder.Sql("ALTER TABLE project_nodes DROP CONSTRAINT IF EXISTS ck_project_nodes_baseline_dates;");

            migrationBuilder.DropForeignKey(
                name: "fk_resources_teams_team_id",
                table: "resources");

            migrationBuilder.DropTable(
                name: "project_node_tags");

            migrationBuilder.DropTable(
                name: "project_skill_requirements");

            migrationBuilder.DropTable(
                name: "resource_skills");

            migrationBuilder.DropTable(
                name: "resource_tags");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "project_nodes");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropIndex(
                name: "ix_resources_team_id",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "resources");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");
        }
    }
}
