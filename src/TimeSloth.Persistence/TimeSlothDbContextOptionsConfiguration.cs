using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TimeSloth.Persistence.Tenancy;

namespace TimeSloth.Persistence;

public sealed class TimeSlothDbContextOptionsConfiguration(
    AuditInterceptor auditInterceptor,
    TenantStampInterceptor tenantStampInterceptor,
    TenantSessionInterceptor tenantSessionInterceptor)
    : IDbContextOptionsConfiguration<TimeSlothDbContext>
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(auditInterceptor, tenantStampInterceptor, tenantSessionInterceptor);
    }
}
