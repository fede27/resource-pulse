using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using ResourcePulse.Common.Tenancy;

namespace ResourcePulse.Persistence.Tenancy;

/// <summary>
/// Publishes the resolved tenant to Postgres as the session variable the
/// row-level-security policies read (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <c>SET</c> and not <c>SET LOCAL</c>.</b> <c>SET LOCAL</c> is scoped to
/// the enclosing transaction; a connection interceptor runs outside one, so the
/// value would be discarded before the first query ever saw it. A session-scoped
/// <c>SET</c> is the correct pairing with connection-open, and it is safe under
/// pooling because Npgsql issues <c>DISCARD ALL</c> when a connection returns to
/// the pool, so the value never leaks into the next borrower.
/// </para>
/// <para>
/// When no tenant is resolved the variable is set to the empty UUID rather than
/// left unset: the policies then match no rows, which is the fail-close outcome.
/// </para>
/// </remarks>
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public const string SessionVariable = "app.tenant_id";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection is not NpgsqlConnection npgsql) return;

        await using var command = npgsql.CreateCommand();
        // set_config() parameterises cleanly; a literal SET statement would have
        // to be string-concatenated.
        command.CommandText = "SELECT set_config(@name, @value, false)";
        command.Parameters.AddWithValue("name", SessionVariable);
        command.Parameters.AddWithValue("value", tenantContext.TenantIdOrEmpty.ToString());
        await command.ExecuteNonQueryAsync(ct);
    }
}
