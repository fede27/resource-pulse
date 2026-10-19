using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeSloth.Domain.Skills;
using TimeSloth.Persistence.Tenancy;

namespace TimeSloth.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(s => s.Id);
        builder.HasTenantId();

        builder.Property(s => s.Name).HasColumnType("citext").IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        // Per-tenant uniqueness: each tenant owns its own skill catalogue.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(Skill.Name))
            .IsUnique()
            .HasDatabaseName("ux_skills_name");
    }
}
