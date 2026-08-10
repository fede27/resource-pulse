using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Domain.Access;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(m => m.Id);
        builder.HasTenantId();

        // citext, like Resource.Email: one unique index then covers
        // "user@x" vs "User@X" without normalizing in the domain.
        builder.Property(m => m.Email).HasColumnType("citext").IsRequired();
        builder.Property(m => m.UserSub).HasMaxLength(256);
        builder.Property(m => m.DisplayName).HasMaxLength(200);
        builder.Property(m => m.GrantedByUserSub).HasMaxLength(256);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Both scoped to the tenant: the same person may hold a membership in two
        // tenants, at different levels.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(Membership.Email))
            .IsUnique()
            .HasDatabaseName("ux_memberships_email");

        // Filtered so that many pending invites (user_sub NULL) coexist.
        builder.HasIndex(TenantModel.TenantIdProperty, nameof(Membership.UserSub))
            .IsUnique()
            .HasFilter("user_sub IS NOT NULL")
            .HasDatabaseName("ux_memberships_user_sub");

        builder.Ignore(m => m.IsPending);
    }
}
