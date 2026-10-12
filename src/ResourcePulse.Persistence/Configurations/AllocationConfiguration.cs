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
        // No anchor-shape CHECK anymore (ADR-0035): with the referent implicit
        // there is no referent column to keep in step with the kind. The two
        // boundary columns are plain enums, and the 20-char string conversion is
        // the only thing that constrains them.
        builder.ToTable("allocations");
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

        // Boundary state (ADR-0035): one enum per edge, nothing else. The
        // referent is the coverage's own project_node_id, already on the row, so
        // there is no referent column, no FK and no shape constraint. The dates
        // the boundaries govern stay on period_start/period_end (denormalized,
        // I9) so nothing downstream reads the boundary state.
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
        System.Linq.Expressions.Expression<Func<Allocation, AnchorKind>> property,
        string edge) =>
        // DB default 'Pinned': a row inserted outside the service keeps an
        // absolute date rather than silently acquiring a tie to its node.
        builder.Property(property)
            .HasColumnName($"{edge}_anchor_kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AnchorKind.Pinned)
            .IsRequired();
}
