using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ResourcePulse.Persistence;

public sealed class ResourcePulseDbContextOptionsConfiguration(AuditInterceptor auditInterceptor)
    : IDbContextOptionsConfiguration<ResourcePulseDbContext>
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(auditInterceptor);
    }
}
