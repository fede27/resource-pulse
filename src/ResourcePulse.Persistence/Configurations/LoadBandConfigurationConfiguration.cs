using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class LoadBandConfigurationConfiguration : IEntityTypeConfiguration<LoadBandConfiguration>
{
    public void Configure(EntityTypeBuilder<LoadBandConfiguration> builder)
    {
        builder.ToTable("load_band_configurations");
        builder.HasKey(c => c.Id);
        builder.HasTenantId();

        // ADR-0029: an org-level singleton becomes a *per-tenant* singleton.
        // The well-known fixed id is gone; this index is what keeps it single.
        builder.HasIndex(TenantModel.TenantIdProperty)
            .IsUnique()
            .HasDatabaseName("ux_load_band_configurations_tenant");

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        // Ordered ladder as an owned child table. The aggregate returns the bands
        // sorted by LowerBound, so storage order is irrelevant. Composite key
        // (owner, lower_bound) — bounds are strictly increasing per band, so they
        // are a natural per-configuration key.
        builder.OwnsMany(c => c.Bands, b =>
        {
            b.ToTable("load_bands");
            b.WithOwner().HasForeignKey("LoadBandConfigurationId");
            b.Property(x => x.LowerBound).HasColumnType("numeric(6,2)").IsRequired();
            b.HasKey("LoadBandConfigurationId", nameof(LoadBand.LowerBound));
            b.Property(x => x.Label).HasMaxLength(100).IsRequired();
        });

        builder.Metadata
            .FindNavigation(nameof(LoadBandConfiguration.Bands))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
