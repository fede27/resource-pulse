using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Tenancy;
using ResourcePulse.Domain.Tenancy;
using ResourcePulse.Persistence.ControlPlane;
using ResourcePulse.Services.Signals;

namespace ResourcePulse.SignalWorker;

/// <summary>
/// Runs the triage detector over every active tenant, on a schedule (ADR-0032 §9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a schedule and not domain events.</b> Signals change without anybody
/// mutating anything: the fence rolls forward from today, so a tentative crosses
/// into the frozen zone while everyone sleeps and a demand enters the committing
/// horizon simply because a day passed. Event-driven detection is therefore
/// structurally insufficient — which is also why ADR-0004 does not need reversing.
/// </para>
/// <para>
/// <b>Why a dedicated process.</b> As a <c>BackgroundService</c> inside the API,
/// N replicas would mean N concurrent sweeps of the same tenant, and the defence
/// would be a distributed lock — more machinery than the process it saves.
/// </para>
/// <para>
/// The timer is the CORRECTNESS guarantee. The resolve-only hook on the plan
/// envelope is only latency for the common case: calendars, closures and capacity
/// all change through controllers that are not the envelope, and they move hours
/// and therefore gaps.
/// </para>
/// </remarks>
public sealed class SignalSweepWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    SweepOptions options,
    ILogger<SignalSweepWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Signal sweep worker started; interval {Interval}, initial delay {Delay}.",
            options.Interval, options.InitialDelay);

        // The API applies pending migrations on startup in development; sweeping
        // before that finishes would just fail noisily on a missing table.
        try
        {
            await Task.Delay(options.InitialDelay, clock, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAllTenantsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A failed round must not kill the loop: the next one is the
                // recovery path, and a dead worker would freeze every tenant's
                // queue on a stale state with nothing saying so.
                logger.LogError(ex, "Signal sweep round failed; retrying at the next interval.");
            }

            try
            {
                await Task.Delay(options.Interval, clock, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAllTenantsAsync(CancellationToken ct)
    {
        List<Tenant> tenants;
        await using (var registryScope = scopeFactory.CreateAsyncScope())
        {
            // The control plane is NOT tenant-scoped and carries no RLS — it is
            // read to ESTABLISH the tenant, so filtering it by tenant would be
            // circular (ADR-0029).
            var controlPlane = registryScope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            tenants = await controlPlane.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active)
                .ToListAsync(ct);
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await SweepTenantAsync(tenant, today, ct);
            }
            catch (Exception ex)
            {
                // One tenant's bad data must not stop the others.
                logger.LogError(ex, "Signal sweep failed for tenant {TenantId} ({TenantName}).",
                    tenant.Id, tenant.Name);
            }
        }
    }

    private async Task SweepTenantAsync(Tenant tenant, DateOnly today, CancellationToken ct)
    {
        // The scope is opened BEFORE the DI scope, and therefore before any
        // DbContext resolves a connection: the session interceptor publishes
        // app.tenant_id when the connection opens, and that is what the RLS
        // policies read. Open it after and the first query runs untenanted.
        using var _ = TenantScope.For(tenant.Id);
        await using var scope = scopeFactory.CreateAsyncScope();

        var detector = scope.ServiceProvider.GetRequiredService<ISignalDetectionService>();
        var result = await detector.SweepAsync(today, ct);

        if (result.IsFailure)
        {
            // The sweep refuses rather than reporting an empty queue when it could
            // not read the plan, so this is an actionable failure and not a quiet
            // no-op: LastSweptAt was deliberately left untouched, and the dashboard
            // will show the queue as stale until a pass actually succeeds.
            logger.LogError(
                "Signal sweep failed for tenant {TenantId}: {Error}. LastSweptAt left unchanged.",
                tenant.Id, result.Error!.Message);
            return;
        }

        var r = result.Value;
        logger.LogInformation(
            "Swept tenant {TenantId}: {Detected} detected, {Created} new, {Worsened} worsened, " +
            "{Resolved} resolved, {Purged} purged, {Live} live.",
            tenant.Id, r.Detected, r.Created, r.Worsened, r.Resolved, r.Purged, r.LiveCount);
    }
}

/// <summary>
/// How often the sweep runs. OPERATIONAL configuration, deliberately not part of
/// <c>SignalPolicy</c>: the cadence is a property of how this process is deployed,
/// not of how an organization plans (ADR-0032 §12).
/// </summary>
public sealed record SweepOptions(TimeSpan Interval, TimeSpan InitialDelay)
{
    // Daily, because the fence rolls in days. The API separately advertises a
    // staleness tolerance to the client so an interrupted worker is visible
    // rather than silently freezing the queue.
    public static readonly SweepOptions Default =
        new(TimeSpan.FromHours(24), TimeSpan.FromSeconds(20));
}
