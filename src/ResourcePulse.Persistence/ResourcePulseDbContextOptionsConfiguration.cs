using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ResourcePulse.Persistence.Tenancy;

namespace ResourcePulse.Persistence;

public sealed class ResourcePulseDbContextOptionsConfiguration(
    AuditInterceptor auditInterceptor,
    TenantStampInterceptor tenantStampInterceptor,
    TenantSessionInterceptor tenantSessionInterceptor)
    : IDbContextOptionsConfiguration<ResourcePulseDbContext>
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(auditInterceptor, tenantStampInterceptor, tenantSessionInterceptor);
    }
}
