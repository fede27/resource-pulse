using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

// Tenant data (ADR-0034 §4): an imposed date belongs to a customer's project.
// The 29th table under RLS — listed explicitly in the AddExternalConstraints
// migration, as every tenant table is.
public sealed class ExternalConstraintConfiguration : IEntityTypeConfiguration<ExternalConstraint>
{
    public void Configure(EntityTypeBuilder<ExternalConstraint> builder)
    {
        builder.ToTable("external_constraints");
        builder.HasKey(c => c.Id);
        builder.HasTenantId();

        builder.Property(c => c.RootProjectId).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Date).HasColumnType("date").IsRequired();
        builder.Property(c => c.Authority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        // Restrict: a root with imposed dates is not deleted by accident.
        builder.HasOne<ProjectNode>()
            .WithMany()
            .HasForeignKey(c => c.RootProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.RootProjectId).HasDatabaseName("ix_external_constraints_root_project_id");
    }
}
