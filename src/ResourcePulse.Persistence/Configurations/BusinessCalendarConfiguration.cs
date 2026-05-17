using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Calendars;

namespace ResourcePulse.Persistence.Configurations;

public sealed class BusinessCalendarConfiguration : IEntityTypeConfiguration<BusinessCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> builder)
    {
        builder.ToTable("business_calendars");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(c => c.UpdatedBy).HasMaxLength(256);

        // Partial unique index — at most one default calendar at any time.
        builder.HasIndex(c => c.IsDefault)
            .IsUnique()
            .HasFilter("is_default = TRUE")
            .HasDatabaseName("ix_business_calendars_is_default_unique");

        builder.OwnsMany(c => c.WorkWindows, w =>
        {
            w.ToTable("business_calendar_work_windows");
            w.WithOwner().HasForeignKey("BusinessCalendarId");
            w.HasKey(x => x.Id);
            w.Property(x => x.DayOfWeek);
            w.Property(x => x.StartTime).HasColumnType("time without time zone");
            w.Property(x => x.EndTime).HasColumnType("time without time zone");
            w.Property(x => x.ValidFrom).HasColumnType("date");
            w.Property(x => x.ValidTo).HasColumnType("date");
            w.HasIndex("BusinessCalendarId", "ValidFrom", "ValidTo");
        });

        builder.Metadata
            .FindNavigation(nameof(BusinessCalendar.WorkWindows))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
