using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// <inheritdoc />
    // Phase 4.1: widens AllocationPercent from (0, 100] to (0, 1000] to admit
    // single-allocation overcommitment as a deliberate signal (see ADR-0013).
    // Two changes:
    //   1. Column type numeric(5,2) -> numeric(6,2) so 1000.00 fits (max 9999.99).
    //   2. Explicit named CHECK constraint enforces (0, 1000]. The column
    //      precision alone would allow up to 9999.99 — the CHECK is the
    //      authoritative range gate.
    public partial class WidenAllocationPercentBound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "allocation_percent",
                table: "allocations",
                type: "numeric(6,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.Sql(@"
                ALTER TABLE allocations
                ADD CONSTRAINT ck_allocations_percent_range
                CHECK (allocation_percent > 0 AND allocation_percent <= 1000);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the CHECK first so the column can be narrowed if any rows
            // currently sit at e.g. 500.00 (they would now violate numeric(5,2)).
            migrationBuilder.Sql("ALTER TABLE allocations DROP CONSTRAINT IF EXISTS ck_allocations_percent_range;");

            migrationBuilder.AlterColumn<decimal>(
                name: "allocation_percent",
                table: "allocations",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)");
        }
    }
}
