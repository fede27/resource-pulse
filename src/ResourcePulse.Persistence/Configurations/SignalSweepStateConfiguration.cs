using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Signals;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class SignalSweepStateConfiguration : IEntityTypeConfiguration<SignalSweepState>
{
    public void Configure(EntityTypeBuilder<SignalSweepState> builder)
    {
        builder.ToTable("signal_sweep_states");
        builder.HasKey(s => s.Id);
        builder.HasTenantId();

        // One row per tenant. Separate from signal_policies on purpose: this is
        // the detector's own bookkeeping, that is Owner-editable configuration.
        builder.HasIndex(TenantModel.TenantIdProperty)
            .IsUnique()
            .HasDatabaseName("ux_signal_sweep_states_tenant");

        // Null until the first sweep completes — and that null is not a missing
        // value to be defaulted away: it IS the "non ho ancora guardato" state
        // the dashboard has to be able to render (ADR-0032 §10).
        builder.Property(s => s.LastSweptAt);
        builder.Property(s => s.LastLiveCount).IsRequired();

        builder.Property(s => s.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
    }
}
