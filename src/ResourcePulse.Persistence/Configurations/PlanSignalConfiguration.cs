using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class PlanSignalConfiguration : IEntityTypeConfiguration<PlanSignal>
{
    public void Configure(EntityTypeBuilder<PlanSignal> builder)
    {
        builder.ToTable("plan_signals");
        builder.HasKey(s => s.Id);
        builder.HasTenantId();

        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.SubjectId);
        builder.Property(s => s.Detection).HasConversion<string>().HasMaxLength(20).IsRequired();

        // ── Observation ─────────────────────────────────────────────────
        builder.Property(s => s.DeadlineAt).HasColumnType("date");
        builder.Property(s => s.Zone).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Magnitude).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(s => s.HardCommitted).IsRequired();

        // uuid[], not a child table. The scope filter needs array containment
        // (`&&`), which is exactly what this shape and the GIN index below give.
        // And keeping the members as an unordered payload is what makes the
        // anti-ranking constraint structural (ADR-0032 §7): a child table with an
        // orderable key would invite precisely the per-person ranking of
        // utilization that UnderBand exists to avoid.
        builder.PrimitiveCollection<List<Guid>>("_touchedRootProjectIds")
            .HasColumnName("touched_root_project_ids")
            .IsRequired();

        builder.PrimitiveCollection<List<Guid>>("_memberSubjectIds")
            .HasColumnName("member_subject_ids")
            .IsRequired();

        builder.Ignore(s => s.TouchedRootProjectIds);
        builder.Ignore(s => s.MemberSubjectIds);

        // ── Change tracking (the "cosa è cambiato" feed) ────────────────
        builder.Property(s => s.FirstDetectedAt).IsRequired();
        builder.Property(s => s.LastObservedAt).IsRequired();
        builder.Property(s => s.LastChangedAt);
        builder.Property(s => s.ResolvedAt);
        builder.Property(s => s.PreviousMagnitude).HasColumnType("numeric(12,2)");
        builder.Property(s => s.PreviousZone).HasConversion<string>().HasMaxLength(20);

        // Stored as an int, deliberately breaking the "enums as string" house
        // convention: SignalChange is [Flags], and a bitmask rendered as
        // "Created, ZoneWorsened" is neither queryable nor stable to reorder.
        builder.Property(s => s.LastChange).HasConversion<int>().IsRequired();

        // Derived from Kind — never stored (ADR-0032 §11).
        builder.Ignore(s => s.Tier);
        builder.Ignore(s => s.Shape);
        builder.Ignore(s => s.IsLive);
        builder.Ignore(s => s.CurrentAcknowledgement);
        builder.Ignore(s => s.IsAcknowledged);

        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        // The queue read is always "live signals for this tenant".
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(PlanSignal.Detection))
            .HasDatabaseName("ix_plan_signals_detection");

        // The change feed reads by transition timestamp.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(PlanSignal.LastChangedAt))
            .HasDatabaseName("ix_plan_signals_last_changed_at");

        // Scope filtering is array overlap against the projects the caller leads.
        builder.HasIndex("_touchedRootProjectIds")
            .HasMethod("gin")
            .HasDatabaseName("ix_plan_signals_touched_roots");

        // NOTE: the identity constraint — (tenant_id, kind, subject_id) UNIQUE
        // among LIVE rows, declared NULLS NOT DISTINCT — is created as raw SQL in
        // the migration. EF cannot express NULLS NOT DISTINCT, and without it two
        // live aggregate rows of the same kind (both with a null subject_id) would
        // not collide and reconciliation would silently duplicate them
        // (ADR-0032 §2).

        // Append-only acknowledgement log. Keyed by (owner, sequence): timestamps
        // can tie, and "the current state is the last entry" has to be exact.
        builder.OwnsMany(s => s.Acknowledgements, a =>
        {
            a.ToTable("signal_acknowledgements");
            a.WithOwner().HasForeignKey("PlanSignalId");
            a.HasKey("PlanSignalId", nameof(SignalAcknowledgement.Sequence));
            // Assigned by the aggregate, never by the database: the sequence has
            // to be dense and known in memory for "the current state is the last
            // entry" to hold before SaveChanges.
            a.Property(x => x.Sequence).ValueGeneratedNever().IsRequired();
            a.Property(x => x.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
            a.Property(x => x.At).IsRequired();
            a.Property(x => x.By).HasMaxLength(256).IsRequired();
            a.Property(x => x.Reason).HasMaxLength(1000);
        });

        builder.Metadata
            .FindNavigation(nameof(PlanSignal.Acknowledgements))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
