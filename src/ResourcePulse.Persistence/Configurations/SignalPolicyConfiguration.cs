using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Configuration;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class SignalPolicyConfiguration : IEntityTypeConfiguration<SignalPolicy>
{
    public void Configure(EntityTypeBuilder<SignalPolicy> builder)
    {
        builder.ToTable("signal_policies", t =>
        {
            t.HasCheckConstraint(
                "ck_signal_policies_retention_positive",
                "resolved_retention_days >= 1");
        });
        builder.HasKey(p => p.Id);
        builder.HasTenantId();

        // The fifth org-level singleton (ADR-0032 §12), per tenant like the other
        // four.
        builder.HasIndex(TenantModel.TenantIdProperty)
            .IsUnique()
            .HasDatabaseName("ux_signal_policies_tenant");

        builder.Property(p => p.ResolvedRetentionDays).IsRequired();

        builder.Property(p => p.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);

        // Stored as a duration, not a date — same treatment as the fence horizons
        // (ADR-0020): the derived deadline is recomputed against the node's
        // planned start at every read.
        builder.OwnsOne(p => p.DecisionLeadTime, d =>
        {
            d.Property(x => x.Value).HasColumnName("decision_lead_time_value").IsRequired();
            d.Property(x => x.Unit).HasColumnName("decision_lead_time_unit")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
        });
        builder.Navigation(p => p.DecisionLeadTime).IsRequired();
    }
}
