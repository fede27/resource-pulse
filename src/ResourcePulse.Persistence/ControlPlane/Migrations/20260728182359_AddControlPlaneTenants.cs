using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.ControlPlane.Migrations
{
    /// <inheritdoc />
    public partial class AddControlPlaneTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "control_plane");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "control_plane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_organization_id = table.Column<string>(type: "citext", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenants_identity_organization_id",
                schema: "control_plane",
                table: "tenants",
                column: "identity_organization_id",
                unique: true);

            // The registry is READ-ONLY to the application role: the request path
            // resolves a tenant from it, it never writes one. Provisioning a
            // tenant is a control-plane operation performed on the owner
            // connection (the dev bootstrap, later a provisioning pipeline).
            //
            // Schema-level USAGE is not covered by ALTER DEFAULT PRIVILEGES, and
            // the schema only exists as of this migration, so the grant lands
            // here. Guarded on the role existing so the migration still applies
            // to environments provisioned without the container init script.
            migrationBuilder.Sql("""
                DO $$
                DECLARE app_role text := 'resourcepulse_app';
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = app_role) THEN
                        EXECUTE format('GRANT USAGE ON SCHEMA control_plane TO %I', app_role);
                        EXECUTE format('GRANT SELECT ON control_plane.tenants TO %I', app_role);
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants",
                schema: "control_plane");
        }
    }
}
