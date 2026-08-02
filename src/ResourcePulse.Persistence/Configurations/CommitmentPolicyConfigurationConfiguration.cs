using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class CommitmentPolicyConfigurationConfiguration : IEntityTypeConfiguration<CommitmentPolicyConfiguration>
{
    public void Configure(EntityTypeBuilder<CommitmentPolicyConfiguration> builder)
    {
        builder.ToTable("commitment_policies");
        builder.HasKey(c => c.Id);
        builder.HasTenantId();

        // ADR-0029: an org-level singleton becomes a *per-tenant* singleton.
        // The well-known fixed id is gone; this index is what keeps it single.
        builder.HasIndex(TenantModel.TenantIdProperty)
            .IsUnique()
            .HasDatabaseName("ux_commitment_policies_tenant");

        // The hard-commit level set is stored as a CSV of level names in a single
        // column. The aggregate is the source of truth and parses/formats it.
        builder.Property<string>("_hardCommitLevels")
            .HasColumnName("hard_commit_levels")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
    }
}
