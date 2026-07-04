using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;

namespace ResourcePulse.Persistence.Configurations;

public sealed class DemandConfiguration : IEntityTypeConfiguration<Demand>
{
    public void Configure(EntityTypeBuilder<Demand> builder)
    {
        builder.ToTable("demands", t =>
        {
            // Best-effort demand has no target (revision §7). When present, the
            // target must be strictly positive — mirrors the EstimatedWork
            // "> 0 when set" CHECK (Phase 4).
            t.HasCheckConstraint(
                "ck_demands_required_hours_positive",
                "required_hours IS NULL OR required_hours > INTERVAL '0'");
        });
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ProjectNodeId).IsRequired();
        builder.Property(d => d.RoleId).IsRequired();

        // interval / TimeSpan?, nullable — the single hours representation along
        // the whole demand→coverage→gap chain (ADR-0026).
        builder.Property(d => d.RequiredHours).HasColumnType("interval");

        builder.Property(d => d.Provenance)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.OwnerResourceId);
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.Property(d => d.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(d => d.UpdatedBy).HasMaxLength(256);

        // Restrict: a demand's node/role/owner cannot be deleted while it is
        // referenced. Coverage (the Allocation) points at the demand with its own
        // Restrict FK, so a demand cannot be deleted while covered either.
        builder.HasOne<ProjectNode>()
            .WithMany()
            .HasForeignKey(d => d.ProjectNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(d => d.OwnerResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // The by-node (and subtree via Path prefix) read is the hot path.
        builder.HasIndex(d => d.ProjectNodeId)
            .HasDatabaseName("ix_demands_project_node_id");

        // NO unique constraint on (ProjectNodeId, RoleId): multiple demand lines
        // per role are intentionally legal ("2 backend devs in Q3" — revision §4,
        // ADR-0016 open question #7 resolved to surface-don't-enforce).
    }
}
