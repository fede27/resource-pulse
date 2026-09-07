using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class SignalVisitConfiguration : IEntityTypeConfiguration<SignalVisit>
{
    public void Configure(EntityTypeBuilder<SignalVisit> builder)
    {
        builder.ToTable("signal_visits");
        builder.HasKey(v => v.Id);
        builder.HasTenantId();

        builder.Property(v => v.UserSub).HasMaxLength(256).IsRequired();
        builder.Property(v => v.LastVisitedAt).IsRequired();

        builder.Property(v => v.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(v => v.UpdatedBy).HasMaxLength(256);

        // One row per (tenant, user) — NOT per (tenant, user, signal). That is the
        // whole point of keeping "seen" separate from the acknowledgement
        // (ADR-0032 §5): one marker per person answers both the unseen dot and
        // the "dal …" heading of the feed.
        //
        // Tenant-scoped even though it is per-user: the same person can belong to
        // several tenants and their reading of one says nothing about the others.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(SignalVisit.UserSub))
            .IsUnique()
            .HasDatabaseName("ux_signal_visits_user_sub");
    }
}
