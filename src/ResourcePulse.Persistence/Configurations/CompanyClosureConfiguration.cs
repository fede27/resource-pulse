using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Persistence.Configurations;

public sealed class CompanyClosureConfiguration : IEntityTypeConfiguration<CompanyClosure>
{
    public void Configure(EntityTypeBuilder<CompanyClosure> builder)
    {
        builder.ToTable("company_closures");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DateFrom).HasColumnType("date").IsRequired();
        builder.Property(c => c.DateTo).HasColumnType("date").IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(500).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(c => new { c.DateFrom, c.DateTo });
    }
}
