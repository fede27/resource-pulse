using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Persistence.Configurations;

public sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("allocations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ResourceId).IsRequired();
        builder.Property(a => a.ProjectNodeId).IsRequired();
        builder.Property(a => a.PeriodStart).HasColumnType("date").IsRequired();
        builder.Property(a => a.PeriodEnd).HasColumnType("date").IsRequired();

        // decimal(6,2): max 9999.99, but domain caps at 1000.00. Widened from
        // numeric(5,2) in the WidenAllocationPercentBound migration (Phase 4.1)
        // so the cap of 1000.00 (overcommitment-as-signal — ADR-0013) fits.
        // The explicit ck_allocations_percent_range CHECK is the real range gate.
        builder.Property(a => a.AllocationPercent)
            .HasColumnType("numeric(6,2)")
            .IsRequired();

        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.Property(a => a.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(a => a.UpdatedBy).HasMaxLength(256);

        // Restrict both: cannot delete a resource or project node while it has allocations.
        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(a => a.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectNode>()
            .WithMany()
            .HasForeignKey(a => a.ProjectNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reads for resource load and project-node load both filter by entity + date range.
        builder.HasIndex(a => new { a.ResourceId, a.PeriodStart, a.PeriodEnd })
            .HasDatabaseName("ix_allocations_resource_id_period");
        builder.HasIndex(a => new { a.ProjectNodeId, a.PeriodStart, a.PeriodEnd })
            .HasDatabaseName("ix_allocations_project_node_id_period");

        // No DB-level overlap constraint: overlapping allocations on the same
        // (ResourceId, ProjectNodeId) are first-class and their rate% sums —
        // see ADR-0014.
    }
}
