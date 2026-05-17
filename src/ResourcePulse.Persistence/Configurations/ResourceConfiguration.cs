using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;

namespace ResourcePulse.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasOne<BusinessCalendar>()
            .WithMany()
            .HasForeignKey(r => r.BusinessCalendarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(r => r.WorkWindows, w =>
        {
            w.ToTable("resource_work_windows");
            w.WithOwner().HasForeignKey("ResourceId");
            w.HasKey(x => x.Id);
            w.Property(x => x.DayOfWeek);
            w.Property(x => x.StartTime).HasColumnType("time without time zone");
            w.Property(x => x.EndTime).HasColumnType("time without time zone");
            w.Property(x => x.ValidFrom).HasColumnType("date");
            w.Property(x => x.ValidTo).HasColumnType("date");
            w.HasIndex("ResourceId", "ValidFrom", "ValidTo");
        });

        builder.OwnsMany(r => r.Adjustments, a =>
        {
            a.ToTable("resource_adjustments");
            a.WithOwner().HasForeignKey("ResourceId");
            a.HasKey(x => x.Id);
            a.Property(x => x.DateFrom).HasColumnType("date");
            a.Property(x => x.DateTo).HasColumnType("date");
            a.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            a.Property(x => x.Hours);
            a.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            a.Property(x => x.Notes).HasMaxLength(2000);
            a.HasIndex("ResourceId", "DateFrom", "DateTo");
        });

        builder.Metadata
            .FindNavigation(nameof(Resource.WorkWindows))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Resource.Adjustments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
