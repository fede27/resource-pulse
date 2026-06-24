using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Persistence.Configurations;

public sealed class TimeFenceConfigurationConfiguration : IEntityTypeConfiguration<TimeFenceConfiguration>
{
    public void Configure(EntityTypeBuilder<TimeFenceConfiguration> builder)
    {
        builder.ToTable("time_fence_configurations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        // Durations stored inline (value + unit columns) — rolling horizons, not
        // dates. The boundary date is recomputed at read time (ADR-0020).
        builder.OwnsOne(c => c.FrozenHorizon, d =>
        {
            d.Property(x => x.Value).HasColumnName("frozen_horizon_value").IsRequired();
            d.Property(x => x.Unit).HasColumnName("frozen_horizon_unit")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
        });
        builder.Navigation(c => c.FrozenHorizon).IsRequired();

        builder.OwnsOne(c => c.SlushyHorizon, d =>
        {
            d.Property(x => x.Value).HasColumnName("slushy_horizon_value").IsRequired();
            d.Property(x => x.Unit).HasColumnName("slushy_horizon_unit")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
        });
        builder.Navigation(c => c.SlushyHorizon).IsRequired();
    }
}
