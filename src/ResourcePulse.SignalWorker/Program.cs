using Microsoft.EntityFrameworkCore.Infrastructure;
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
// perfectly configured. This process writes; it must be constrained. The
// derivation is shared with the API (AppDbConnection, in Persistence): it used to
// be a second copy here, which is one copy too many for a rule this load-bearing.
var connections = AppDbConnection.Resolve(builder.Configuration, builder.Environment.IsDevelopment());

builder.AddNpgsqlDbContext<ResourcePulseDbContext>(AppDbConnection.ResourceName, settings =>
{
    if (connections.Application is not null) settings.ConnectionString = connections.Application;
});

builder.AddNpgsqlDbContext<ControlPlaneDbContext>(
    AppDbConnection.ResourceName,
    settings =>
    {
        if (connections.Application is not null) settings.ConnectionString = connections.Application;
    },
    ControlPlaneDbContext.ConfigureOptions);

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
