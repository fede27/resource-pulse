using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// ADR-0038 — `ProjectType` stops mixing "who pays" with "what kind of work" and
    /// becomes Run / Grow / Transform. Who pays stays on `client`, untouched.
    ///
    /// No schema change: `type` is a varchar holding the enum name, so the decision is
    /// entirely in the translation below (§7 of the ADR):
    ///
    ///   Maintenance -> Run
    ///   Customer    -> Grow
    ///   Investment  -> Transform
    ///   Internal    -> Transform   (an approximation: the field had no reader and
    ///                               the data has never been in production)
    ///
    /// Phases and work packages carry NULL and are left alone.
    public partial class ReclassifyProjectTypeAsRunGrowTransform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE project_nodes
                   SET type = CASE type
                         WHEN 'Maintenance' THEN 'Run'
                         WHEN 'Customer'    THEN 'Grow'
                         WHEN 'Investment'  THEN 'Transform'
                         WHEN 'Internal'    THEN 'Transform'
                         ELSE type
                       END
                 WHERE type IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Internal and Investment both became Transform, so Down cannot tell them
            // apart: Transform goes back to Investment, the other two are exact.
            migrationBuilder.Sql("""
                UPDATE project_nodes
                   SET type = CASE type
                         WHEN 'Run'       THEN 'Maintenance'
                         WHEN 'Grow'      THEN 'Customer'
                         WHEN 'Transform' THEN 'Investment'
                         ELSE type
                       END
                 WHERE type IS NOT NULL;
                """);
        }
    }
}
