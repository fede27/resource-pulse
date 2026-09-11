using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Persistence.Configurations;

public sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("allocations", t =>
        {
            // Anchor shape (ADR-0034 §1): the referent columns are typed by kind.
            // NodeStart/NodeEnd carry a node, External carries a constraint,
            // Pinned and ResourceAvailability carry nothing — so a dangling or
            // mis-typed referent cannot be stored, whatever the service does.
            t.HasCheckConstraint("ck_allocations_start_anchor_shape", AnchorShapeSql("start"));
            t.HasCheckConstraint("ck_allocations_end_anchor_shape", AnchorShapeSql("end"));
        });
        builder.HasKey(a => a.Id);

        // Coverage model (Phase 5.1, ADR-0025): every allocation covers a demand
        // with a real resource. No form XOR / placeholder columns anymore.
        builder.Property(a => a.DemandId).IsRequired();
        builder.Property(a => a.ResourceId).IsRequired();
        builder.Property(a => a.ProjectNodeId).IsRequired(); // denormalized == Demand.ProjectNodeId (I8)
        builder.Property(a => a.PeriodStart).HasColumnType("date").IsRequired();
        builder.Property(a => a.PeriodEnd).HasColumnType("date").IsRequired();

        // decimal(6,2): domain caps at 1000.00 (overcommitment-as-signal, ADR-0013).
        builder.Property(a => a.AllocationPercent)
            .HasColumnType("numeric(6,2)")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.Property(a => a.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        // Boundary anchors (ADR-0034): two owned value objects stored inline as
        // flat columns. The dates they govern stay on period_start/period_end
        // (denormalized, I9) so nothing downstream reads the anchor.
        ConfigureAnchor(builder, a => a.StartAnchor, "start");
        ConfigureAnchor(builder, a => a.EndAnchor, "end");

        // Restrict everywhere: cannot delete a demand, resource or project node
        // while coverage references it. A demand is thus undeletable while covered
        // (the service surfaces this as Conflict — "detach coverage first").
        builder.HasOne<Demand>()
            .WithMany()
            .HasForeignKey(a => a.DemandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(a => a.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectNode>()
            .WithMany()
            .HasForeignKey(a => a.ProjectNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reads for resource load and project-node load filter by entity + date range.
        builder.HasIndex(a => new { a.ResourceId, a.PeriodStart, a.PeriodEnd })
            .HasDatabaseName("ix_allocations_resource_id_period");
        builder.HasIndex(a => new { a.ProjectNodeId, a.PeriodStart, a.PeriodEnd })
            .HasDatabaseName("ix_allocations_project_node_id_period");
        // The demand-coverage read joins coverage to its demand.
        builder.HasIndex(a => a.DemandId)
            .HasDatabaseName("ix_allocations_demand_id");

        // No DB-level overlap constraint: overlapping allocations on the same
        // (ResourceId, ProjectNodeId) are first-class and their rate% sums (ADR-0014).
    }

    private static void ConfigureAnchor(
        EntityTypeBuilder<Allocation> builder,
        System.Linq.Expressions.Expression<Func<Allocation, BoundaryAnchor?>> navigation,
        string edge)
    {
        builder.OwnsOne(navigation, o =>
        {
            // DB default 'Pinned' is what every pre-existing row gets at migration
            // time: an absolute date that moves only when edited — exactly what
            // those rows were before anchoring existed.
            o.Property(x => x.Kind)
                .HasColumnName($"{edge}_anchor_kind")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(AnchorKind.Pinned)
                .IsRequired();
            o.Property(x => x.NodeId).HasColumnName($"{edge}_anchor_node_id");
            o.Property(x => x.ConstraintId).HasColumnName($"{edge}_anchor_constraint_id");

            // Restrict: a referent with anchored edges hanging off it is not
            // deleted by accident — pin them first.
            o.HasOne<ProjectNode>()
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Restrict);
            o.HasOne<ExternalConstraint>()
                .WithMany()
                .HasForeignKey(x => x.ConstraintId)
                .OnDelete(DeleteBehavior.Restrict);

            // Propagation and the "N anchored edges" counts filter here.
            o.HasIndex(x => x.NodeId).HasDatabaseName($"ix_allocations_{edge}_anchor_node_id");
            o.HasIndex(x => x.ConstraintId).HasDatabaseName($"ix_allocations_{edge}_anchor_constraint_id");
        });
        builder.Navigation(navigation).IsRequired();
    }

    private static string AnchorShapeSql(string edge) =>
        $"({edge}_anchor_kind IN ('NodeStart', 'NodeEnd') AND {edge}_anchor_node_id IS NOT NULL AND {edge}_anchor_constraint_id IS NULL) " +
        $"OR ({edge}_anchor_kind = 'External' AND {edge}_anchor_constraint_id IS NOT NULL AND {edge}_anchor_node_id IS NULL) " +
        $"OR ({edge}_anchor_kind IN ('Pinned', 'ResourceAvailability') AND {edge}_anchor_node_id IS NULL AND {edge}_anchor_constraint_id IS NULL)";
}
