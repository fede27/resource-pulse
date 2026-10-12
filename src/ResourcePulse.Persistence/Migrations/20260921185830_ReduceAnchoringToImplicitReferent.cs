using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourcePulse.Persistence.Migrations
{
    /// ADR-0035 — the referent of a boundary becomes IMPLICIT: it is the node the
    /// coverage already sits on. Five anchor kinds collapse to two, and the four
    /// referent columns, their FKs, their indexes and the two shape CHECKs go with
    /// them.
    ///
    /// The DATES ARE NOT TOUCHED. What is lost is the tie, and only where it could
    /// not be re-expressed; the translation below is the whole decision, in SQL:
    ///
    ///   NodeStart on a START edge, pointing at the coverage's OWN node   -> FollowsNode
    ///   NodeEnd   on an END   edge, pointing at the coverage's OWN node  -> FollowsNode
    ///   everything else (cross-node anchors, NodeEnd on a start edge,
    ///   ResourceAvailability, External)                                 -> Pinned
    ///
    /// A row that becomes Pinned keeps the date it had, which is exactly what
    /// pinning means everywhere else in the model: the tie breaks, the date stays.
    /// So no block moves when this migration runs, and none moves afterwards until
    /// someone replans a node.
    public partial class ReduceAnchoringToImplicitReferent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Collapse the kinds BEFORE the referent columns are dropped — the
            // translation reads them. Order matters and this cannot move below.
            migrationBuilder.Sql(CollapseSql("start", "NodeStart"));
            migrationBuilder.Sql(CollapseSql("end", "NodeEnd"));

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_external_constraints_end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_external_constraints_start_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_project_nodes_end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_allocations_project_nodes_start_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_start_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropIndex(
                name: "ix_allocations_start_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_end_anchor_shape",
                table: "allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_allocations_start_anchor_shape",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "end_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "end_anchor_node_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "start_anchor_constraint_id",
                table: "allocations");

            migrationBuilder.DropColumn(
                name: "start_anchor_node_id",
                table: "allocations");
        }

        // An edge follows its node only if it was anchored to the matching
        // boundary OF THAT NODE. `own` is the kind that survives on this edge:
        // a start edge keeps NodeStart, an end edge keeps NodeEnd. Anything else
        // is pinned where it stands.
        private static string CollapseSql(string edge, string own) => $"""
            UPDATE allocations
               SET {edge}_anchor_kind = CASE
                     WHEN {edge}_anchor_kind = '{own}'
                      AND {edge}_anchor_node_id = project_node_id THEN 'FollowsNode'
                     ELSE 'Pinned'
                   END;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The ties are not recoverable — they were dropped, not archived. Every
            // boundary comes back Pinned on the date it currently holds, which is
            // the one reading that loses no information about WHERE blocks are.
            // Must run before the shape CHECKs: 'FollowsNode' is not a value they
            // admit.
            migrationBuilder.Sql(
                "UPDATE allocations SET start_anchor_kind = 'Pinned', end_anchor_kind = 'Pinned';");

            migrationBuilder.AddColumn<Guid>(
                name: "end_anchor_constraint_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "end_anchor_node_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "start_anchor_constraint_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "start_anchor_node_id",
                table: "allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocations_end_anchor_constraint_id",
                table: "allocations",
                column: "end_anchor_constraint_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_end_anchor_node_id",
                table: "allocations",
                column: "end_anchor_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_start_anchor_constraint_id",
                table: "allocations",
                column: "start_anchor_constraint_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_start_anchor_node_id",
                table: "allocations",
                column: "start_anchor_node_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_end_anchor_shape",
                table: "allocations",
                sql: "(end_anchor_kind IN ('NodeStart', 'NodeEnd') AND end_anchor_node_id IS NOT NULL AND end_anchor_constraint_id IS NULL) OR (end_anchor_kind = 'External' AND end_anchor_constraint_id IS NOT NULL AND end_anchor_node_id IS NULL) OR (end_anchor_kind IN ('Pinned', 'ResourceAvailability') AND end_anchor_node_id IS NULL AND end_anchor_constraint_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_allocations_start_anchor_shape",
                table: "allocations",
                sql: "(start_anchor_kind IN ('NodeStart', 'NodeEnd') AND start_anchor_node_id IS NOT NULL AND start_anchor_constraint_id IS NULL) OR (start_anchor_kind = 'External' AND start_anchor_constraint_id IS NOT NULL AND start_anchor_node_id IS NULL) OR (start_anchor_kind IN ('Pinned', 'ResourceAvailability') AND start_anchor_node_id IS NULL AND start_anchor_constraint_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_external_constraints_end_anchor_constraint_id",
                table: "allocations",
                column: "end_anchor_constraint_id",
                principalTable: "external_constraints",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_external_constraints_start_anchor_constraint_id",
                table: "allocations",
                column: "start_anchor_constraint_id",
                principalTable: "external_constraints",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_project_nodes_end_anchor_node_id",
                table: "allocations",
                column: "end_anchor_node_id",
                principalTable: "project_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_allocations_project_nodes_start_anchor_node_id",
                table: "allocations",
                column: "start_anchor_node_id",
                principalTable: "project_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
