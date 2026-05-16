using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain;

namespace ResourcePulse.Persistence.Configurations;

public sealed class PingConfiguration : IEntityTypeConfiguration<Ping>
{
    public void Configure(EntityTypeBuilder<Ping> builder)
    {
        builder.ToTable("pings");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Message).HasMaxLength(500).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);
    }
}
