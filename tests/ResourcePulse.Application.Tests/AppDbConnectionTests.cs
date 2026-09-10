using Microsoft.Extensions.Configuration;
using Npgsql;
using ResourcePulse.Persistence;

namespace ResourcePulse.Application.Tests;

// The rule that makes row-level security real: the request path and the sweep
// connect as a NOSUPERUSER role, never as the owner Aspire hands us. It used to
// be two copies, one per host, with no test on either.
public class AppDbConnectionTests
{
    private static IConfiguration Config(string? owner, string? role, string? password = "s3cret")
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{AppDbConnection.ResourceName}"] = owner,
            ["Tenancy:AppDbRole"] = role,
            ["Tenancy:AppDbPassword"] = password
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void TheApplicationConnectionSwapsTheCredentialsAndKeepsTheOwnerIntact()
    {
        var owner = "Host=db;Port=5432;Database=resourcepulse;Username=postgres;Password=owner-pw";

        var connections = AppDbConnection.Resolve(Config(owner, "resourcepulse_app"), isDevelopment: false);

        connections.Owner.Should().Be(owner);
        var app = new NpgsqlConnectionStringBuilder(connections.Application);
        app.Username.Should().Be("resourcepulse_app");
        app.Password.Should().Be("s3cret");
        app.Database.Should().Be("resourcepulse");
        app.Host.Should().Be("db");
    }

    [Fact]
    public void AnOwnerStringWithNoDatabasePinsItToTheOwnerUsername()
    {
        // The trap this method exists for. Aspire's connection string carries no
        // Database=, and Npgsql defaults it to the USERNAME — so swapping the
        // username without pinning first silently retargets a database named
        // after the application role, which does not exist.
        var owner = "Host=db;Username=postgres;Password=owner-pw";

        var connections = AppDbConnection.Resolve(Config(owner, "resourcepulse_app"), isDevelopment: false);

        new NpgsqlConnectionStringBuilder(connections.Application).Database.Should().Be("postgres");
    }

    [Fact]
    public void DevelopmentMayRunWithoutTheRole()
    {
        // The init script may not have created it yet. A null application
        // connection means "open the owner", which the callers do by leaving
        // Aspire's default in place.
        var owner = "Host=db;Database=resourcepulse;Username=postgres";

        var connections = AppDbConnection.Resolve(Config(owner, role: null), isDevelopment: true);

        connections.Owner.Should().Be(owner);
        connections.Application.Should().BeNull();
    }

    [Theory]
    [InlineData("Host=db;Username=postgres", null)]
    [InlineData(null, "resourcepulse_app")]
    [InlineData(null, null)]
    public void OutsideDevelopmentAMissingRoleRefusesToStart(string? owner, string? role)
    {
        // Starting unconstrained is worse than not starting: every tenant policy
        // would be inert and nothing would say so.
        var act = () => AppDbConnection.Resolve(Config(owner, role), isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Tenancy:AppDbRole is required*");
    }
}
