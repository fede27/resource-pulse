using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class BucketingDefaultsConfiguration : IEntityTypeConfiguration<BucketingDefaults>
{
    public void Configure(EntityTypeBuilder<BucketingDefaults> builder)
    {
        builder.ToTable("bucketing_defaults");
        builder.HasKey(c => c.Id);
        builder.HasTenantId();

        // ADR-0029: an org-level singleton becomes a *per-tenant* singleton.
        // The well-known fixed id is gone; this index is what keeps it single.
        builder.HasIndex(TenantModel.TenantIdProperty)
            .IsUnique()
            .HasDatabaseName("ux_bucketing_defaults_tenant");

        builder.Property(c => c.PrimaryGrain).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.SecondaryGrain).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
    }
}
