using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ResourcePulse.Common.Auth;

namespace ResourcePulse.Persistence;

// Used by EF Core tools (dotnet ef migrations add) at design time.
// The real connection string and DI come from Aspire at runtime.
public sealed class ResourcePulseDbContextFactory : IDesignTimeDbContextFactory<ResourcePulseDbContext>
{
    public ResourcePulseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResourcePulseDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=resourcepulse;Username=postgres;Password=postgres")
            .AddInterceptors(new AuditInterceptor(new AnonymousAccessor()));
        return new ResourcePulseDbContext(optionsBuilder.Options);
    }

    private sealed class AnonymousAccessor : ICurrentUserAccessor
    {
        public CurrentUser User => CurrentUser.Anonymous;
        public bool IsAuthenticated => false;
    }
}
