using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TimeSloth.Common.Auth;

namespace TimeSloth.Persistence;

// Used by EF Core tools (dotnet ef migrations add) at design time.
// The real connection string and DI come from Aspire at runtime.
public sealed class TimeSlothDbContextFactory : IDesignTimeDbContextFactory<TimeSlothDbContext>
{
    public TimeSlothDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimeSlothDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=timesloth;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditInterceptor(new AnonymousAccessor(), TimeProvider.System));
        return new TimeSlothDbContext(optionsBuilder.Options);
    }

    private sealed class AnonymousAccessor : ICurrentUserAccessor
    {
        public CurrentUser User => CurrentUser.Anonymous;
        public bool IsAuthenticated => false;
        public string? AuthenticationScheme => null;
    }
}
