using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Teams;

namespace ResourcePulse.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");
        builder.HasKey(t => t.Id);

        // citext = case-insensitive text, Postgres-native. Migration ensures the
        // extension exists; uniqueness then works via a plain unique index.
        builder.Property(t => t.Name).HasColumnType("citext").IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("ux_teams_name");
    }
}
