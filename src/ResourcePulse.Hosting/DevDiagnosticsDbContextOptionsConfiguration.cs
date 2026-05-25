using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ResourcePulse.Persistence;

namespace ResourcePulse.Hosting;

// Dev-only EF diagnostics. Surfaces SQL parameter values and detailed EF errors
// so SaveChanges failure modes are debuggable. Kept out of the Persistence layer
// because Persistence has no awareness of hosting environment. Registered only
// when builder.Environment.IsDevelopment() in Program.cs — EF composes every
// IDbContextOptionsConfiguration<T> registered for the same DbContext, so this
// runs after the base ResourcePulseDbContextOptionsConfiguration.
public sealed class DevDiagnosticsDbContextOptionsConfiguration
    : IDbContextOptionsConfiguration<ResourcePulseDbContext>
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }
}
