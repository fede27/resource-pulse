using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberships : Migration
    {
        // The authorization store (ADR-0030). It is tenant data — a grant belongs
        // to exactly one tenant and must be invisible and unwritable from any
        // other — so it takes a row-level-security policy like the other 22
        // tables, and the count goes to 23.
        //
        // Adding a table to the schema is deliberately not enough to make it
        // tenant-scoped: AddTenantIsolationAndRowLevelSecurity keeps its table
        // list explicit precisely so this decision has to be made out loud. It is
        // made here, and it matters more than usual: a leaked or plantable row in
        // THIS table is a cross-tenant privilege grant.
        private const string Table = "memberships";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", nullable: false),
                    user_sub = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_by_user_sub = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id",
                table: "memberships",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_memberships_email",
                table: "memberships",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_memberships_user_sub",
                table: "memberships",
                columns: new[] { "tenant_id", "user_sub" },
                unique: true,
                filter: "user_sub IS NOT NULL");

            // ── Row-level security ───────────────────────────────────────────
            // Verbatim the shape of AddTenantIsolationAndRowLevelSecurity: FORCE
            // so the policy binds the owner too, and USING *and* WITH CHECK so a
            // request can neither read nor plant a grant outside its own tenant.
            migrationBuilder.Sql($"ALTER TABLE {Table} ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql($"ALTER TABLE {Table} FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql($"""
                CREATE POLICY {Table}_tenant_isolation ON {Table}
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP POLICY IF EXISTS {Table}_tenant_isolation ON {Table};");

            migrationBuilder.DropTable(
                name: "memberships");
        }
    }
}
