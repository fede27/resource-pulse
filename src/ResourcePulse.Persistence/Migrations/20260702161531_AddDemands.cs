using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_hours = table.Column<TimeSpan>(type: "interval", nullable: true),
                    provenance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demands", x => x.id);
                    table.CheckConstraint("ck_demands_required_hours_positive", "required_hours IS NULL OR required_hours > INTERVAL '0'");
                    table.ForeignKey(
                        name: "fk_demands_project_nodes_project_node_id",
                        column: x => x.project_node_id,
                        principalTable: "project_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_demands_resources_owner_resource_id",
                        column: x => x.owner_resource_id,
                        principalTable: "resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_demands_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_demands_owner_resource_id",
                table: "demands",
                column: "owner_resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_demands_project_node_id",
                table: "demands",
                column: "project_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_demands_role_id",
                table: "demands",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demands");
        }
    }
}
