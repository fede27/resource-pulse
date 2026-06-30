using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Domain.Resources;
using ResourcePulse.Domain.Skills;
using ResourcePulse.Domain.Tags;

namespace ResourcePulse.Persistence.Configurations;

public sealed class ProjectNodeConfiguration : IEntityTypeConfiguration<ProjectNode>
{
    public void Configure(EntityTypeBuilder<ProjectNode> builder)
    {
        builder.ToTable("project_nodes");
        builder.HasKey(p => p.Id);

        // ── Tree ────────────────────────────────────────────────────────────
        builder.Property(p => p.NodeType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(100);
        builder.Property(p => p.Path).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.Depth).IsRequired();

        // Self-referencing parent FK; Restrict to prevent cascading deletes through the tree.
        builder.HasOne<ProjectNode>()
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Project-only fields (nullable on Phase/WorkPackage) ─────────────
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.CommitmentLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        // Customer / committente (M1). Free text, project-only; the "non-root ⇒
        // NULL" rule is enforced by a CHECK added in the migration, matching the
        // ck_project_nodes_project_only_fields pattern.
        builder.Property(p => p.Client).HasMaxLength(500);

        // ── Planning (Project|Phase only) ───────────────────────────────────
        builder.Property(p => p.PlanningMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.EstimatedWork).HasColumnType("interval");

        // Lead can be cleared when the resource is deleted (we don't want to block resource cleanup).
        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(p => p.LeadResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Dates ───────────────────────────────────────────────────────────
        builder.Property(p => p.BaselineStart).HasColumnType("date");
        builder.Property(p => p.BaselineEnd).HasColumnType("date");
        builder.Property(p => p.BaselinedAt).HasColumnType("timestamp with time zone");
        builder.Property(p => p.PlannedStart).HasColumnType("date");
        builder.Property(p => p.PlannedEnd).HasColumnType("date");
        builder.Property(p => p.ActualStart).HasColumnType("date");
        builder.Property(p => p.ActualEnd).HasColumnType("date");

        // ── Audit ───────────────────────────────────────────────────────────
        builder.Property(p => p.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        // ── Indexes ─────────────────────────────────────────────────────────
        // text_pattern_ops makes LIKE 'prefix%' subtree queries index-eligible regardless of collation.
        builder.HasIndex(p => p.Path)
            .HasDatabaseName("ix_project_nodes_path")
            .HasOperators("text_pattern_ops");

        builder.HasIndex(p => p.ParentId).HasDatabaseName("ix_project_nodes_parent_id");
        builder.HasIndex(p => new { p.NodeType, p.Status }).HasDatabaseName("ix_project_nodes_node_type_status");

        // Non-root siblings: unique code among children of the same parent.
        builder.HasIndex(p => new { p.ParentId, p.Code })
            .IsUnique()
            .HasFilter("parent_id IS NOT NULL AND code IS NOT NULL")
            .HasDatabaseName("ux_project_nodes_parent_code");

        // Roots: globally unique code (since Postgres treats NULL parents as non-conflicting).
        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasFilter("parent_id IS NULL AND code IS NOT NULL")
            .HasDatabaseName("ux_project_nodes_root_code");

        // ── Owned: SkillRequirements ────────────────────────────────────────
        builder.OwnsMany(p => p.SkillRequirements, r =>
        {
            r.ToTable("project_skill_requirements");
            r.WithOwner().HasForeignKey(x => x.ProjectNodeId);
            r.HasKey(x => new { x.ProjectNodeId, x.SkillId });
            r.Property(x => x.MinLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
            r.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            r.Property(x => x.UpdatedBy).HasMaxLength(256);
            r.HasOne<Skill>()
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
            r.HasIndex(x => x.SkillId).HasDatabaseName("ix_project_skill_requirements_skill_id");
        });

        // ── Owned: Tags ─────────────────────────────────────────────────────
        builder.OwnsMany(p => p.Tags, t =>
        {
            t.ToTable("project_node_tags");
            t.WithOwner().HasForeignKey(x => x.ProjectNodeId);
            t.HasKey(x => new { x.ProjectNodeId, x.TagId });
            t.HasOne<Tag>()
                .WithMany()
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasIndex(x => x.TagId).HasDatabaseName("ix_project_node_tags_tag_id");
        });

        builder.Metadata
            .FindNavigation(nameof(ProjectNode.SkillRequirements))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata
            .FindNavigation(nameof(ProjectNode.Tags))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
