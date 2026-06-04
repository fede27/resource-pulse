using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Calendars;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Roles;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;
using ResourcePulse.Domain.Teams;

namespace ResourcePulse.Persistence.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.UserSub).HasMaxLength(256);
        // Email uses citext so a single unique index handles case-insensitive
        // duplicates ("user@x" vs "User@X"). Filtered to allow many NULLs.
        builder.Property(r => r.Email).HasColumnType("citext");
        builder.Property(r => r.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(r => r.UserSub)
            .IsUnique()
            .HasFilter("user_sub IS NOT NULL")
            .HasDatabaseName("ux_resources_user_sub");

        builder.HasIndex(r => r.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL")
            .HasDatabaseName("ux_resources_email");

        builder.HasOne<BusinessCalendar>()
            .WithMany()
            .HasForeignKey(r => r.BusinessCalendarId)
            .OnDelete(DeleteBehavior.Restrict);

        // Team FK is nullable: resources without a team are legal.
        // Restrict prevents deleting a team that still has resources.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(r => r.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.TeamId).HasDatabaseName("ix_resources_team_id");

        // Role FK is nullable. Restrict so deleting a role used by resources
        // surfaces as a 409 rather than silently nulling the assignment.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(r => r.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.RoleId).HasDatabaseName("ix_resources_role_id");

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

        // ResourceSkill — owned join, composite PK, FK to Skill (Restrict).
        builder.OwnsMany(r => r.Skills, s =>
        {
            s.ToTable("resource_skills");
            s.WithOwner().HasForeignKey(x => x.ResourceId);
            s.HasKey(x => new { x.ResourceId, x.SkillId });
            s.Property(x => x.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
            s.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            s.Property(x => x.ReviewedByResourceId);
            s.Property(x => x.ReviewedAt).HasColumnType("timestamp with time zone");
            s.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            s.Property(x => x.UpdatedBy).HasMaxLength(256);
            s.HasOne<Skill>()
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
            // Reviewer is itself a Resource. Restrict: deleting a supervisor
            // must not silently strip approval provenance from history.
            s.HasOne<Resource>()
                .WithMany()
                .HasForeignKey(x => x.ReviewedByResourceId)
                .OnDelete(DeleteBehavior.Restrict);
            s.HasIndex(x => x.SkillId).HasDatabaseName("ix_resource_skills_skill_id");
            s.HasIndex(x => x.ReviewedByResourceId).HasDatabaseName("ix_resource_skills_reviewed_by_resource_id");
        });

        // ResourceTag — owned join, composite PK, FK to Tag (Cascade).
        builder.OwnsMany(r => r.Tags, t =>
        {
            t.ToTable("resource_tags");
            t.WithOwner().HasForeignKey(x => x.ResourceId);
            t.HasKey(x => new { x.ResourceId, x.TagId });
            t.HasOne<Tag>()
                .WithMany()
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasIndex(x => x.TagId).HasDatabaseName("ix_resource_tags_tag_id");
        });

        builder.Metadata
            .FindNavigation(nameof(Resource.WorkWindows))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Resource.Adjustments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Resource.Skills))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(Resource.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
