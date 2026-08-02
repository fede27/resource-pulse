using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResourcePulse.DevBootstrap;
using ResourcePulse.Domain.Tenancy;
using ResourcePulse.Persistence.ControlPlane;

// Deterministic, idempotent dev bootstrap (ADR-0029).
//
// Runs after Zitadel reports healthy and before the API and SPA start. It:
//   1. authenticates with the machine-user PAT Zitadel wrote at first init,
//   2. ensures the project and its two applications exist (search-then-create),
//   3. registers the Zitadel organization in OUR control-plane tenant registry,
//   4. writes the resulting ids where the API and the SPA read them.
//
// Re-running it is a no-op. It is dev-only: it never runs in production, where
// the identity configuration is provisioned out of band.

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Must mirror the API's registration exactly: without the naming convention the
// context looks for "Tenants", and without the history-table override it looks
// for the wrong migrations table — either way it does not see the schema the
// control-plane migration actually created.
//
// This one connects with the OWNER credentials Aspire provides (unlike the API,
// which uses the restricted application role): it applies the control-plane
// migration and writes the tenant row.
builder.AddNpgsqlDbContext<ControlPlaneDbContext>(
    "resourcepulse-db",
    configureDbContextOptions: options => options
        .UseSnakeCaseNamingConvention()
        .UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(
            "__ef_migrations_history", ControlPlaneDbContext.SchemaName)));

using var host = builder.Build();
var log = host.Services.GetRequiredService<ILogger<Program>>();
var config = host.Services.GetRequiredService<IConfiguration>();
var ct = CancellationToken.None;

var issuer = Required(config, "Zitadel:Issuer");
var machineKeyPath = Required(config, "Zitadel:MachineKeyPath");
var apiConfigPath = Path.GetFullPath(Required(config, "Bootstrap:ApiConfigPath"));
var spaEnvPath = Path.GetFullPath(Required(config, "Bootstrap:SpaEnvPath"));

// The PAT file appears only after Zitadel's first-instance provisioning has run.
// The health check fires slightly earlier, so wait for the file rather than
// racing it.
var pat = await ReadPersonalAccessTokenAsync(machineKeyPath, log, ct);

using var http = new HttpClient { BaseAddress = new Uri(issuer) };
var zitadel = new ZitadelManagementClient(http, pat);

var (organizationId, organizationName) = await zitadel.GetOrganizationAsync(ct);
log.LogInformation("Zitadel organization {Name} ({Id})", organizationName, organizationId);

var projectId = await zitadel.EnsureProjectAsync("resource-pulse", ct);

var spaClientId = await zitadel.EnsureSpaApplicationAsync(
    projectId,
    "resource-pulse-spa",
    [
        Required(config, "Bootstrap:SpaRedirectUri"),
        Required(config, "Bootstrap:SpaSilentRenewUri")
    ],
    [Required(config, "Bootstrap:SpaPostLogoutUri")],
    ct);

var apiClientId = await zitadel.EnsureApiApplicationAsync(projectId, "resource-pulse-api", ct);

// Without this, registering a user fails with Errors.SMTPConfig.NotFound and the
// account is never initialised. The target is the dev mail sink, so nothing
// leaves the machine.
var smtpProviderId = await zitadel.EnsureSmtpProviderAsync(
    description: "dev-mailpit",
    host: Required(config, "Bootstrap:SmtpHost"),
    senderAddress: Required(config, "Bootstrap:SmtpSenderAddress"),
    senderName: Required(config, "Bootstrap:SmtpSenderName"),
    ct);

log.LogInformation("Zitadel SMTP provider {SmtpProviderId} active, delivering to {SmtpHost}",
    smtpProviderId, config["Bootstrap:SmtpHost"]);

log.LogInformation("Zitadel project {ProjectId}; SPA {SpaClientId}; API {ApiClientId}",
    projectId, spaClientId, apiClientId);

// ── Control plane: map the organization to a tenant of ours ──────────────────
// The organization id is a LOOKUP KEY, never the tenant id itself: keeping our
// own identifier means a tenant can be re-federated without rewriting a single
// row of planning data.
var controlPlane = host.Services.GetRequiredService<ControlPlaneDbContext>();
await controlPlane.Database.MigrateAsync(ct);

var tenant = await controlPlane.Tenants
    .FirstOrDefaultAsync(t => t.IdentityOrganizationId == organizationId, ct);

if (tenant is null)
{
    tenant = Tenant.Create(organizationId, organizationName);
    controlPlane.Tenants.Add(tenant);
    await controlPlane.SaveChangesAsync(ct);
    log.LogInformation("Registered tenant {TenantId} for organization {OrganizationId}",
        tenant.Id, organizationId);
}
else
{
    log.LogInformation("Tenant {TenantId} already mapped to organization {OrganizationId}",
        tenant.Id, organizationId);
}

// ── Publish the configuration ────────────────────────────────────────────────
var apiConfig = new
{
    Zitadel = new
    {
        Issuer = issuer,
        ProjectId = projectId,
        ApiClientId = apiClientId,
        SpaClientId = spaClientId
    }
};

Directory.CreateDirectory(Path.GetDirectoryName(apiConfigPath)!);
await File.WriteAllTextAsync(
    apiConfigPath,
    JsonSerializer.Serialize(apiConfig, new JsonSerializerOptions { WriteIndented = true }),
    ct);

var spaEnv = new StringBuilder()
    .AppendLine("# Generated by ResourcePulse.DevBootstrap. Do not edit; do not commit.")
    .AppendLine($"VITE_OIDC_AUTHORITY={issuer}")
    .AppendLine($"VITE_OIDC_CLIENT_ID={spaClientId}")
    .AppendLine($"VITE_OIDC_REDIRECT_URI={Required(config, "Bootstrap:SpaRedirectUri")}")
    .AppendLine($"VITE_OIDC_SILENT_REDIRECT_URI={Required(config, "Bootstrap:SpaSilentRenewUri")}")
    .AppendLine($"VITE_OIDC_POST_LOGOUT_REDIRECT_URI={Required(config, "Bootstrap:SpaPostLogoutUri")}")
    // Zitadel does not honour a bare `audience` parameter: the API's audience is
    // requested through this reserved scope, which puts the project id (and its
    // API applications) into the token's `aud`.
    .AppendLine($"VITE_OIDC_PROJECT_SCOPE=urn:zitadel:iam:org:project:id:{projectId}:aud")
    .ToString();

Directory.CreateDirectory(Path.GetDirectoryName(spaEnvPath)!);
await File.WriteAllTextAsync(spaEnvPath, spaEnv, ct);

log.LogInformation("Bootstrap complete. API config: {ApiConfig}; SPA env: {SpaEnv}",
    apiConfigPath, spaEnvPath);

return 0;

static string Required(IConfiguration config, string key) =>
    config[key] ?? throw new InvalidOperationException($"Missing required configuration '{key}'.");

static async Task<string> ReadPersonalAccessTokenAsync(string path, ILogger log, CancellationToken ct)
{
    var deadline = DateTimeOffset.UtcNow.AddMinutes(2);

    while (DateTimeOffset.UtcNow < deadline)
    {
        if (File.Exists(path))
        {
            var token = (await File.ReadAllTextAsync(path, ct)).Trim();
            if (token.Length > 0) return token;
        }

        log.LogInformation("Waiting for Zitadel machine-user PAT at {Path}...", path);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }

    throw new InvalidOperationException(
        $"Zitadel did not produce a machine-user PAT at '{path}'. If the Zitadel container " +
        "was initialised before ZITADEL_FIRSTINSTANCE_PATPATH was configured, its database " +
        "already exists and first-instance provisioning will not run again: drop the " +
        "'zitadel-db' database (or the Postgres volume) and restart.");
}
