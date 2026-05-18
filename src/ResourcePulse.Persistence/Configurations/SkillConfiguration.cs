using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Skills;

namespace ResourcePulse.Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasColumnType("citext").IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("ux_skills_name");
    }
}
