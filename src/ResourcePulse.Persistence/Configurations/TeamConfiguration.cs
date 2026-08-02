using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Teams;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(t => t.Id);
        builder.HasTenantId();

        // citext = case-insensitive text, Postgres-native. Migration ensures the
        // extension exists; uniqueness then works via a plain unique index.
        builder.Property(t => t.Name).HasColumnType("citext").IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        // Per-tenant uniqueness: two tenants may each have a team named "Delivery".
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(Team.Name))
            .IsUnique()
            .HasDatabaseName("ux_teams_name");
    }
}
