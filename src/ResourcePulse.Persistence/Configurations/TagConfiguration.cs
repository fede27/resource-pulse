using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(t => t.Id);
        builder.HasTenantId();

        // Tag names are stored already normalized (lower-invariant + trimmed)
        // by the domain factory, so a plain unique text column suffices.
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        // Per-tenant uniqueness: the shared tag catalogue is shared *within* a
        // tenant, never across tenants.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(Tag.Name))
            .IsUnique()
            .HasDatabaseName("ux_tags_name");
    }
}
