using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Services.Plan;

// Plan mutation under one mechanism (ADR-0018). Dispatches on the runtime
// command type, applies the domain kernel (ADR-0019), and either commits or —
// when DryRun — returns the computed consequence WITHOUT persisting.
//
// dryRun mechanism (ADR-0018 §2): pure in-memory application of the domain
// primitives, projecting PlanBlockChange from the aggregates; SaveChanges runs
// only when !DryRun. New entities are added / removed only on commit; on a
// dryRun the ChangeTracker is cleared so tracked mutations are discarded.
//
// Cross-aggregate invariants ported verbatim from the retired AllocationService
// write paths: I1 (planning-level node), I3 (resource active — assigned only),
// I4 (root not Closed/Cancelled), I6 (Hard ⇒ root committed). Overlap is never
// re-checked: it sums and is surfaced, not enforced (ADR-0014, ADR-0019 §4).
public sealed class PlanCommandService(
    ResourcePulseDbContext db,
    ICapacityQueryService capacity,
    ICommitmentPolicyService commitmentPolicy) : IPlanCommandService
{
    public Task<ServiceResult<PlanCommandResult>> ExecuteAsync(
        PlanCommand command, CancellationToken ct = default) =>
        command switch
        {
            CreateCommand c => CreateAsync(c, ct),
            CreateByHoursCommand c => CreateByHoursAsync(c, ct),
            CreatePlaceholderCommand c => CreatePlaceholderAsync(c, ct),
            EditCommand c => EditAsync(c, ct),
            SplitAtCommand c => SplitAtAsync(c, ct),
            ChangeRateFromCommand c => ChangeRateFromAsync(c, ct),
            MoveCommand c => MoveAsync(c, ct),
            RetargetCommand c => RetargetAsync(c, ct),
            ResizeCommand c => ResizeAsync(c, ct),
            ShiftFromCommand c => ShiftFromAsync(c, ct),
            ConvertToPlaceholderCommand c => ConvertToPlaceholderAsync(c, ct),
            ReassignCommand c => ReassignAsync(c, ct),
            ChangeStatusCommand c => ChangeStatusAsync(c, ct),
            DeleteCommand c => DeleteAsync(c, ct),
            _ => Task.FromResult(Fail(ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["kind"] = [$"Unknown command type {command.GetType().Name}."]
            })))
        };

    // ── Creation ─────────────────────────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> CreateAsync(CreateCommand c, CancellationToken ct)
    {
        var (nodeErr, _) = await LoadPlanningNodeAsync(c.ProjectNodeId, ct);
        if (nodeErr is { } e1) return Fail(e1);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(c.ProjectNodeId, ct) is { } e4)
            return Fail(e4);

        return await CreateAssignedAsync(
            c, c.ResourceId, c.ProjectNodeId, c.PeriodStart, c.PeriodEnd, c.Percent, c.Status, c.Notes, "create", ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateByHoursAsync(CreateByHoursCommand c, CancellationToken ct)
    {
        var (nodeErr, _) = await LoadPlanningNodeAsync(c.ProjectNodeId, ct);
        if (nodeErr is { } e1) return Fail(e1);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(c.ProjectNodeId, ct) is { } e4)
            return Fail(e4);

        var cap = await CapacityInWindowAsync(c.ResourceId, c.PeriodStart, c.PeriodEnd, ct);
        if (cap.IsFailure) return Fail(cap.Error!);
        if (cap.Value <= TimeSpan.Zero)
            return Fail(ServiceError.Conflict(
                "Cannot allocate hours: resource has zero capacity in the requested window."));

        decimal percent;
        try { percent = AllocationResolver.PercentForHours(c.TargetHours, cap.Value); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        if (percent > Allocation.MaxAllocationPercent)
            return Fail(ServiceError.Conflict(
                $"Resolved percent {percent} exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                "Widen the window or reduce target hours."));

        return await CreateAssignedAsync(
            c, c.ResourceId, c.ProjectNodeId, c.PeriodStart, c.PeriodEnd, percent, c.Status, c.Notes, "createByHours", ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreatePlaceholderAsync(
        CreatePlaceholderCommand c, CancellationToken ct)
    {
        var (nodeErr, _) = await LoadPlanningNodeAsync(c.ProjectNodeId, ct);
        if (nodeErr is { } e1) return Fail(e1);
        if (await CheckPlaceholderRefsAsync(c.RoleId, c.OwnerResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(c.ProjectNodeId, ct) is { } e4)
            return Fail(e4);

        Allocation a;
        try
        {
            a = Allocation.CreatePlaceholder(
                c.ProjectNodeId, c.PeriodStart, c.PeriodEnd, c.Percent, c.RoleId, c.OwnerResourceId, c.Notes, c.Status);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "createPlaceholder",
            [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateAssignedAsync(
        PlanCommand cmd, Guid resourceId, Guid projectNodeId, DateOnly start, DateOnly end,
        decimal percent, AllocationStatus status, string? notes, string kind, CancellationToken ct)
    {
        Allocation a;
        try { a = Allocation.Create(resourceId, projectNodeId, start, end, percent, notes, status); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(cmd, kind, [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct);
    }

    // ── Edit in place ────────────────────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> EditAsync(EditCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        try
        {
            a.ChangePeriod(c.PeriodStart, c.PeriodEnd);
            a.ChangePercent(c.AllocationPercent);
            a.Annotate(c.Notes);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "edit", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // ── Span operations (ADR-0019) ───────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> SplitAtAsync(SplitAtCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        Allocation second;
        try { second = a.SplitAt(c.Date, "Split"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "splitAt",
            [ToChange(a, PlanChangeKind.Modified), ToChange(second, PlanChangeKind.Created)],
            toAdd: [second], toRemove: null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ChangeRateFromAsync(ChangeRateFromCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        Allocation second;
        try { second = a.ChangeRateFrom(c.Date, c.NewRate, "ChangeRateFrom"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "changeRateFrom",
            [ToChange(a, PlanChangeKind.Modified), ToChange(second, PlanChangeKind.Created)],
            toAdd: [second], toRemove: null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> MoveAsync(MoveCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        try { a.Shift(c.DeltaDays, "Move"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "move", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> RetargetAsync(RetargetCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        decimal newPercent;
        switch (c.Mode)
        {
            case MoveMode.KeepPercent:
                newPercent = a.AllocationPercent;
                break;

            case MoveMode.KeepHours:
            {
                if (a.ResourceId is null)
                    return Fail(ServiceError.Conflict(
                        "Cannot retarget with KeepHours on a placeholder allocation: no resource ⇒ no capacity reference. " +
                        "Use KeepPercent or assign the placeholder first."));

                var resourceId = a.ResourceId.Value;
                var oldCap = await CapacityInWindowAsync(resourceId, a.PeriodStart, a.PeriodEnd, ct);
                if (oldCap.IsFailure) return Fail(oldCap.Error!);
                var newCap = await CapacityInWindowAsync(resourceId, c.NewPeriodStart, c.NewPeriodEnd, ct);
                if (newCap.IsFailure) return Fail(newCap.Error!);

                if (newCap.Value <= TimeSpan.Zero)
                    return Fail(ServiceError.Conflict(
                        "Cannot retarget with KeepHours: resource has zero capacity in the new window."));
                if (oldCap.Value <= TimeSpan.Zero)
                    return Fail(ServiceError.Conflict(
                        "Cannot retarget with KeepHours: the original window now has zero capacity " +
                        "(calendar changed since the allocation was created). Use KeepPercent or correct the calendar first."));

                var oldHours = AllocationResolver.HoursForPercent(a.AllocationPercent, oldCap.Value);
                try { newPercent = AllocationResolver.PercentForHours(oldHours, newCap.Value); }
                catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

                if (newPercent > Allocation.MaxAllocationPercent)
                    return Fail(ServiceError.Conflict(
                        $"KeepHours retarget would require {newPercent}% in the new window, " +
                        $"which exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                        "Widen the new window or accept a different rate (use KeepPercent)."));
                break;
            }

            default:
                return Fail(ServiceError.Validation(new Dictionary<string, string[]>
                {
                    [nameof(RetargetCommand.Mode)] = [$"Unknown move mode {c.Mode}."]
                }));
        }

        try
        {
            a.ChangePeriod(c.NewPeriodStart, c.NewPeriodEnd, "Retarget");
            a.ChangePercent(newPercent);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "retarget", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ResizeAsync(ResizeCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        try { a.Resize(c.NewPeriodStart, c.NewPeriodEnd, "Resize"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "resize", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ShiftFromAsync(ShiftFromCommand c, CancellationToken ct)
    {
        // Lane = resource × project_node, downstream in time. Tracked load so the
        // mutations persist. Placeholders (ResourceId null) are excluded by the
        // ResourceId filter — per-resource lane = assigned offer (ADR-0016 §5).
        var lane = await db.Allocations
            .Where(a => a.ResourceId == c.ResourceId
                     && a.ProjectNodeId == c.ProjectNodeId
                     && a.PeriodStart >= c.FromDate)
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        if (lane.Count == 0)
            return await FinalizeAsync(c, "shiftFrom", [], null, null, ct);

        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e) return Fail(e);

        try
        {
            // Same delta on every block ⇒ relative gaps preserved. Any resulting
            // overlap with blocks before FromDate is allowed and sums.
            foreach (var a in lane) a.Shift(c.DeltaDays, "ShiftFrom");
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        var changes = lane.Select(a => ToChange(a, PlanChangeKind.Modified)).ToList();
        return await FinalizeAsync(c, "shiftFrom", changes, null, null, ct);
    }

    // ── Form & status transitions ────────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> ConvertToPlaceholderAsync(
        ConvertToPlaceholderCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (a.IsPlaceholder) return Fail(ServiceError.Conflict("Allocation is already a placeholder."));
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e1) return Fail(e1);
        if (await CheckPlaceholderRefsAsync(c.RoleId, c.OwnerResourceId, ct) is { } e2) return Fail(e2);

        try { a.ConvertToPlaceholder(c.RoleId, c.OwnerResourceId); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "convertToPlaceholder", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ReassignAsync(ReassignCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (!a.IsPlaceholder)
            return Fail(ServiceError.Conflict("Only a placeholder allocation can be assigned to a resource."));
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e1) return Fail(e1);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e2) return Fail(e2);

        try { a.AssignTo(c.ResourceId); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "reassign", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ChangeStatusAsync(ChangeStatusCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(a.ProjectNodeId, ct) is { } e)
            return Fail(e);

        try { a.ChangeStatus(c.Status, c.Reason); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "changeStatus", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> DeleteAsync(DeleteCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        a.MarkDeleted();
        // Project the pre-delete state before removal.
        var change = ToChange(a, PlanChangeKind.Deleted);
        return await FinalizeAsync(c, "delete", [change], toAdd: null, toRemove: [a], ct);
    }

    // ── Persistence boundary ─────────────────────────────────────────────────

    // Commits (Add/Remove + SaveChanges) only when !DryRun. On a dryRun the
    // ChangeTracker is cleared so tracked mutations are discarded and nothing is
    // persisted (verified explicitly in the integration tests). `changes` is
    // built by the caller from the in-memory aggregates BEFORE this point, so it
    // reflects the would-be state in both modes.
    private async Task<ServiceResult<PlanCommandResult>> FinalizeAsync(
        PlanCommand cmd, string kind, IReadOnlyList<PlanBlockChange> changes,
        IReadOnlyList<Allocation>? toAdd, IReadOnlyList<Allocation>? toRemove, CancellationToken ct)
    {
        if (!cmd.DryRun)
        {
            if (toAdd is not null)
                foreach (var a in toAdd) await db.Allocations.AddAsync(a, ct);
            if (toRemove is not null)
                db.Allocations.RemoveRange(toRemove);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            db.ChangeTracker.Clear();
        }

        return ServiceResult<PlanCommandResult>.Success(new PlanCommandResult
        {
            CommandKind = kind,
            DryRun = cmd.DryRun,
            Committed = !cmd.DryRun,
            Changes = changes
        });
    }

    private async Task<Allocation?> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Allocations.FindAsync([id], ct);

    private static ServiceResult<PlanCommandResult> NotFound(Guid id) =>
        ServiceResult<PlanCommandResult>.NotFound($"Allocation {id} not found.");

    private static ServiceResult<PlanCommandResult> Fail(ServiceError e) =>
        ServiceResult<PlanCommandResult>.Failure(e);

    private static PlanBlockChange ToChange(Allocation a, PlanChangeKind kind) => new()
    {
        Kind = kind,
        Id = a.Id,
        ResourceId = a.ResourceId,
        IsPlaceholder = a.IsPlaceholder,
        ProjectNodeId = a.ProjectNodeId,
        PeriodStart = a.PeriodStart,
        PeriodEnd = a.PeriodEnd,
        AllocationPercent = a.AllocationPercent,
        Status = a.Status,
        RoleId = a.RoleId,
        OwnerResourceId = a.OwnerResourceId,
        Notes = a.Notes
    };

    // ── Cross-aggregate guards (ported from AllocationService) ────────────────

    // I1: node exists and is at capacity-planning level. Returns its Path.
    private async Task<(ServiceError? err, string? path)> LoadPlanningNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var n = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId)
            .Select(p => new { p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (n is null)
            return (ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"ProjectNode {nodeId} does not exist."]
            }), null);

        if (n.NodeType != ProjectNodeType.Project && n.NodeType != ProjectNodeType.Phase)
            return (ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"Allocations are only allowed on Project or Phase nodes (got {n.NodeType})."]
            }), null);

        return (null, n.Path);
    }

    // I4: root project not Closed/Cancelled. By node id (resolves the path).
    private async Task<ServiceError?> CheckProjectStatusByNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return null; // node vanished; surfaced elsewhere

        var rootIdStr = path.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdStr, out var rootId))
            return ServiceError.Failure("ProjectNode has an invalid materialized path.");

        var status = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootId).Select(p => p.Status).FirstOrDefaultAsync(ct);

        if (status == ProjectStatus.Closed || status == ProjectStatus.Cancelled)
            return ServiceError.Conflict($"Project root is {status} and cannot accept new or modified allocations.");

        return null;
    }

    // I3: resource exists and is active.
    private async Task<ServiceError?> CheckResourceActiveAsync(Guid resourceId, CancellationToken ct)
    {
        var info = await db.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId).Select(r => new { r.IsActive }).FirstOrDefaultAsync(ct);

        if (info is null)
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ResourceId"] = [$"Resource {resourceId} does not exist."]
            });
        if (!info.IsActive)
            return ServiceError.Conflict($"Resource {resourceId} is inactive and cannot be allocated.");
        return null;
    }

    // I6: Hard requires the root project committed (Committed/Critical).
    private async Task<ServiceError?> CheckHardCommitmentAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return ServiceError.Failure($"ProjectNode {nodeId} disappeared while validating I6.");

        var rootIdStr = path.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdStr, out var rootId))
            return ServiceError.Failure("ProjectNode has an invalid materialized path.");

        var level = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootId).Select(p => (CommitmentLevel?)p.CommitmentLevel).FirstOrDefaultAsync(ct);

        // I6 threshold read from CommitmentPolicy (ADR-0020) — no longer cabled.
        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        if (!policy.IsHardCommitted(level))
        {
            var allowed = string.Join(", ", policy.HardCommitLevels);
            return ServiceError.Conflict(
                $"Allocation cannot be set to Hard: the project root commitment level is " +
                $"'{level?.ToString() ?? "Unspecified"}'. Hard requires one of: {allowed}.");
        }
        return null;
    }

    // Placeholder references exist (RoleId required; OwnerResourceId optional).
    // RoleId targets the Role catalogue (ADR-0021 / M2), not Skill.
    private async Task<ServiceError?> CheckPlaceholderRefsAsync(Guid roleId, Guid? ownerResourceId, CancellationToken ct)
    {
        if (!await db.Roles.AnyAsync(r => r.Id == roleId, ct))
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["RoleId"] = [$"Role {roleId} does not exist."]
            });

        if (ownerResourceId is Guid o && !await db.Resources.AnyAsync(r => r.Id == o, ct))
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["OwnerResourceId"] = [$"Resource {o} does not exist."]
            });
        return null;
    }

    private async Task<ServiceResult<TimeSpan>> CapacityInWindowAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct)
    {
        var cap = await capacity.GetForResourceAsync(resourceId, from, toInclusive, ct);
        if (cap.IsFailure) return ServiceResult<TimeSpan>.Failure(cap.Error!);

        var total = TimeSpan.Zero;
        foreach (var d in cap.Value) total += d.Hours;
        return ServiceResult<TimeSpan>.Success(total);
    }
}
