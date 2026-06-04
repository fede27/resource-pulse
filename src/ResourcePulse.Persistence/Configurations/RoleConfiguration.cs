using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Roles;

namespace ResourcePulse.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);

        // citext = case-insensitive text, Postgres-native. The Phase 3 migration
        // already installed the extension for Teams; the unique index below
        // therefore enforces "Developer" == "developer" without any extra work.
        builder.Property(r => r.Name).HasColumnType("citext").IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ux_roles_name");
    }
}
