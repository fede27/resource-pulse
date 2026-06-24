using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;

namespace ResourcePulse.Persistence.Configurations;

public sealed class BucketingDefaultsConfiguration : IEntityTypeConfiguration<BucketingDefaults>
{
    public void Configure(EntityTypeBuilder<BucketingDefaults> builder)
    {
        builder.ToTable("bucketing_defaults");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PrimaryGrain).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.SecondaryGrain).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);
    }
}
