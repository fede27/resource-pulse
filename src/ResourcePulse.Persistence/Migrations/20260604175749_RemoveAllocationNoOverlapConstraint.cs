using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    // ADR-0014: overlapping blocks on the same (resource, project_node) are
    // now first-class — overlapping rate% values sum rather than collide. The
    // service-level I2 check and the DB EXCLUDE constraint that enforced
    // no-overlap (ADR-0008) are removed together. btree_gist stays installed —
    // dropping an extension fails in presence of other dependencies, and future
    // features may want it.
    public partial class RemoveAllocationNoOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE allocations DROP CONSTRAINT IF EXISTS allocations_no_overlap;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-create the EXCLUDE constraint. This will FAIL if any
            // overlapping allocations exist in the table when the rollback
            // runs — by design, since the whole point of the rollback is to
            // restore the no-overlap rule. Resolve the overlaps before
            // rolling back. btree_gist is expected to still be installed
            // (the AddAllocations migration's Down does not drop it).
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
    }
}
