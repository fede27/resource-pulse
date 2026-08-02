using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Tenancy;

namespace ResourcePulse.Persistence.ControlPlane.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);

        // citext: identity-provider organization ids are opaque strings; matching
        // them case-insensitively avoids a whole class of "works on my instance"
        // lookup misses. Matches the codebase convention for natural keys.
        builder.Property(t => t.IdentityOrganizationId)
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(t => t.Name).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();

        // One identity-provider organization maps to at most one tenant. This is
        // the uniqueness the fail-close resolver relies on.
        builder.HasIndex(t => t.IdentityOrganizationId)
            .IsUnique()
            .HasDatabaseName("ux_tenants_identity_organization_id");
    }
}
