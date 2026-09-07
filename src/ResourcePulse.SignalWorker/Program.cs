using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using ResourcePulse.Common.Auth;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain;
using ResourcePulse.Persistence;
using ResourcePulse.Persistence.ControlPlane;
using ResourcePulse.Persistence.Tenancy;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Load;
using ResourcePulse.Services.Signals;
using ResourcePulse.SignalWorker;

// The triage detector's home (ADR-0032 §9). A dedicated process rather than a
// BackgroundService in the API: N API replicas would mean N concurrent sweeps of
// the same tenant, and the defence would be a distributed lock — more machinery
// than the process it saves.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);

// No requests here, so no HttpContext and no token: the tenant comes ONLY from an
// explicit TenantScope, and the author of what the sweep writes is a machine.
builder.Services.AddSingleton<ITenantContext, AmbientTenantContext>();
builder.Services.AddSingleton<ICurrentUserAccessor, SystemUserAccessor>();

// Same interceptors as the API — the sweep writes tenant data, so the stamp and
// the session publication are not optional here.
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddSingleton<TenantStampInterceptor>();
builder.Services.AddSingleton<TenantSessionInterceptor>();
builder.Services.AddSingleton<IDbContextOptionsConfiguration<ResourcePulseDbContext>,
    ResourcePulseDbContextOptionsConfiguration>();

// ── The application role, for the same reason as in the API ──────────────────
// A Postgres superuser bypasses RLS unconditionally, so a sweep running as the
// owner would read and write across every tenant while the policies looked
// perfectly configured. This process writes; it must be constrained.
var ownerConnectionString = builder.Configuration.GetConnectionString("resourcepulse-db");
var appDbRole = builder.Configuration["Tenancy:AppDbRole"];
var appDbPassword = builder.Configuration["Tenancy:AppDbPassword"];

string? appConnectionString = null;
if (!string.IsNullOrWhiteSpace(ownerConnectionString) && !string.IsNullOrWhiteSpace(appDbRole))
{
    var owner = new NpgsqlConnectionStringBuilder(ownerConnectionString);
    appConnectionString = new NpgsqlConnectionStringBuilder(ownerConnectionString)
    {
        // Pin the database before swapping the user: Aspire's connection string
        // carries no Database=, and Npgsql would then default it to the username.
        Database = string.IsNullOrEmpty(owner.Database) ? owner.Username : owner.Database,
        Username = appDbRole,
        Password = appDbPassword
    }.ConnectionString;
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Tenancy:AppDbRole is required outside Development: without a non-superuser role the " +
        "row-level-security policies do not constrain the sweep.");
}

builder.AddNpgsqlDbContext<ResourcePulseDbContext>("resourcepulse-db", settings =>
{
    if (appConnectionString is not null) settings.ConnectionString = appConnectionString;
});

builder.AddNpgsqlDbContext<ControlPlaneDbContext>(
    "resourcepulse-db",
    settings =>
    {
        if (appConnectionString is not null) settings.ConnectionString = appConnectionString;
    },
    options => options
        .UseSnakeCaseNamingConvention()
        .UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(
            "__ef_migrations_history", ControlPlaneDbContext.SchemaName)));

builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

// Exactly the detector's dependency graph — nothing else from the API surface.
// It composes the same batch read models the boards use (ADR-0028).
builder.Services.AddScoped<ICapacityQueryService, LiveCapacityQueryService>();
builder.Services.AddScoped<ILoadQueryService, LiveLoadQueryService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<ILoadBandConfigurationService, LoadBandConfigurationService>();
builder.Services.AddScoped<ITimeFenceConfigurationService, TimeFenceConfigurationService>();
builder.Services.AddScoped<ICommitmentPolicyService, CommitmentPolicyService>();
builder.Services.AddScoped<ISignalPolicyService, SignalPolicyService>();
builder.Services.AddScoped<ISignalDetectionService, SignalDetectionService>();

builder.Services.AddSingleton(new SweepOptions(
    TimeSpan.FromHours(builder.Configuration.GetValue("Signals:SweepIntervalHours", 24)),
    TimeSpan.FromSeconds(builder.Configuration.GetValue("Signals:InitialDelaySeconds", 20))));

builder.Services.AddHostedService<SignalSweepWorker>();

var host = builder.Build();
host.Run();
