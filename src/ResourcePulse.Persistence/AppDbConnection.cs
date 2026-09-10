using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ResourcePulse.Persistence;

/// <summary>
/// The two sets of credentials this system connects with, and the one place that
/// derives one from the other.
/// </summary>
public sealed record DatabaseConnections(string? Owner, string? Application);

/// <summary>
/// Resolves the application (non-superuser) connection from the owner connection
/// Aspire provides.
/// </summary>
/// <remarks>
/// <para>
/// A Postgres superuser bypasses row-level security unconditionally, and
/// <c>FORCE ROW LEVEL SECURITY</c> does not constrain one either — so a process
/// that writes tenant data as the owner reads and writes across every tenant
/// while the policies look perfectly configured (ADR-0029). Both the API and the
/// signal worker therefore run on a dedicated <c>NOSUPERUSER NOBYPASSRLS</c>
/// role; DDL stays on the owner.
/// </para>
/// <para>
/// This lives in Persistence, and not in either host, because both hosts need it
/// and it had drifted into two copies — a duplication with a real trap inside it
/// (see the <c>Database</c> assignment below). Two independently-maintained
/// answers to "which credentials constrain us?" is not a formatting problem.
/// </para>
/// </remarks>
public static class AppDbConnection
{
    /// <summary>The Aspire resource both hosts open.</summary>
    public const string ResourceName = "resourcepulse-db";

    public static DatabaseConnections Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var ownerConnectionString = configuration.GetConnectionString(ResourceName);
        var appDbRole = configuration["Tenancy:AppDbRole"];
        var appDbPassword = configuration["Tenancy:AppDbPassword"];

        if (string.IsNullOrWhiteSpace(ownerConnectionString) || string.IsNullOrWhiteSpace(appDbRole))
        {
            // Development may run without the role (the init script may not have
            // created it yet); anywhere else, starting unconstrained is worse
            // than not starting.
            if (!isDevelopment)
                throw new InvalidOperationException(
                    "Tenancy:AppDbRole is required outside Development: without a non-superuser role " +
                    "the row-level-security policies do not constrain this process.");

            return new DatabaseConnections(ownerConnectionString, null);
        }

        var owner = new NpgsqlConnectionStringBuilder(ownerConnectionString);

        var applicationConnectionString = new NpgsqlConnectionStringBuilder(ownerConnectionString)
        {
            // Pin the database BEFORE swapping the user. Aspire's connection string
            // carries no Database=, and Npgsql then defaults it to the username — so
            // changing the username would silently retarget a database named after
            // the application role, which does not exist.
            Database = string.IsNullOrEmpty(owner.Database) ? owner.Username : owner.Database,
            Username = appDbRole,
            Password = appDbPassword
        }.ConnectionString;

        return new DatabaseConnections(ownerConnectionString, applicationConnectionString);
    }
}
